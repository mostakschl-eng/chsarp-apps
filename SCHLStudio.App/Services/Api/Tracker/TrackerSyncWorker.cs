using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.Configuration;

namespace SCHLStudio.App.Services.Api.Tracker
{
    /// <summary>
    /// Background sync worker that sends queued work logs to the backend.
    /// Runs every 2 seconds (mirrors Python SyncWorker).
    /// Max 3 retries per item before discarding.
    /// </summary>
    public sealed class TrackerSyncWorker
    {
        private enum SendResult
        {
            Success = 0,
            RetryableFailure = 1,
            PermanentFailure = 2
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private const int SyncIntervalMs = 2000;
        private const int MaxRetries = 3;
        private const int HttpTimeoutSeconds = 30;

        private readonly TrackerLocalQueue _queue;
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private readonly Func<bool> _isAuthenticated;
        private readonly object _lifecycleLock = new();
        private readonly string _trackerSecret;

        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        private readonly SemaphoreSlim _trigger = new(0, 1);

        public TrackerSyncWorker(
            TrackerLocalQueue queue,
            HttpClient httpClient,
            string apiBaseUrl,
            Func<bool> isAuthenticated)
        {
            _queue = queue;
            _httpClient = httpClient;
            _apiBaseUrl = (apiBaseUrl ?? string.Empty).TrimEnd('/');
            _isAuthenticated = isAuthenticated;
            _trackerSecret = (AppConfig.GetTrackerSecret() ?? string.Empty).Trim();
        }

        /// <summary>
        /// Start the background sync loop.
        /// </summary>
        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (_workerTask is not null && !_workerTask.IsCompleted)
                {
                    return;
                }

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _workerTask = Task.Run(() => RunLoop(_cts.Token));
            }

            Debug.WriteLine("[TrackerSync] Worker started");
        }

        /// <summary>
        /// Stop the background sync loop gracefully.
        /// </summary>
        public void Stop()
        {
            Task? workerToWait = null;
            CancellationTokenSource? ctsToDispose = null;

            try
            {
                lock (_lifecycleLock)
                {
                    workerToWait = _workerTask;
                    ctsToDispose = _cts;
                    _workerTask = null;
                    _cts = null;
                }

                ctsToDispose?.Cancel();
                TriggerSync();

                if (workerToWait is not null)
                {
                    try
                    {
                        _ = workerToWait.Wait(TimeSpan.FromSeconds(4));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TrackerSync] Stop wait exception: {ex.GetType().Name}");
                        NonCriticalLog.IncrementAndLog("Tracker", "Worker.Stop.Wait", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerSync] Stop exception (expected): {ex.GetType().Name}");
                NonCriticalLog.IncrementAndLog("Tracker", "Worker.Stop", ex);
            }
            finally
            {
                try
                {
                    ctsToDispose?.Dispose();
                }
                catch (Exception ex)
                {
                    NonCriticalLog.IncrementAndLog("Tracker", "Worker.Stop.DisposeCts", ex);
                }

                Debug.WriteLine("[TrackerSync] Worker stopped");
            }
        }

        /// <summary>
        /// Wake up the worker immediately to process new items.
        /// </summary>
        public void TriggerSync()
        {
            try
            {
                if (_trigger.CurrentCount == 0)
                    _trigger.Release();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerSync] TriggerSync non-critical: {ex.Message}");
                NonCriticalLog.IncrementAndLog("Tracker", "Worker.TriggerSync", ex);
            }
        }

        private async Task RunLoop(CancellationToken ct)
        {
            Debug.WriteLine("[TrackerSync] Background loop started");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Wait for trigger or timeout (2 seconds)
                    try
                    {
                        await _trigger.WaitAsync(TimeSpan.FromMilliseconds(SyncIntervalMs), ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Skip if not authenticated
                    if (!_isAuthenticated())
                    {
                        continue;
                    }

                    // Process one item at a time
                    await ProcessQueue(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerSync] Loop error: {ex.Message}");
                    NonCriticalLog.IncrementAndLog("Tracker", "Worker.RunLoop", ex);
                }
            }

            Debug.WriteLine("[TrackerSync] Background loop ended");
        }

        private async Task ProcessQueue(CancellationToken ct)
        {
            var items = _queue.PeekAll();
            if (items.Count == 0) return;

            for (var i = 0; i < items.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var item = items[i];

                try
                {
                    var result = await SendItem(item, ct).ConfigureAwait(false);

                    if (result == SendResult.Success)
                    {
                        _queue.RemoveAt(0); // Always remove from front
                        Debug.WriteLine($"[TrackerSync] ✅ Synced: {GetItemName(item)}");
                    }
                    else if (result == SendResult.PermanentFailure)
                    {
                        // Permanent errors (typically HTTP 4xx except rate-limit/timeouts) should not block the queue.
                        _queue.BackupToFailedAndRemoveAt(0, reason: "permanent_failure");
                        Debug.WriteLine($"[TrackerSync] ❌ Discarded (permanent): {GetItemName(item)}");
                        continue;
                    }
                    else
                    {
                        var newRetry = _queue.IncrementRetry(0);
                        if (newRetry >= MaxRetries)
                        {
                            try
                            {
                                AppDataLog.LogEvent(
                                    area: "Tracker",
                                    operation: "Sync.Discard",
                                    level: "error",
                                    data: new System.Collections.Generic.Dictionary<string, string?>
                                    {
                                        ["item"] = GetItemName(item),
                                        ["retryCount"] = newRetry.ToString()
                                    });
                            }
                            catch
                            {
                            }

                            // Backup full payload before removing from queue (prevents data loss)
                            _queue.BackupToFailedAndRemoveAt(0, reason: "max_retries");
                            Debug.WriteLine($"[TrackerSync] ❌ Discarded (max retries): {GetItemName(item)}");
                        }
                        else
                        {
                            try
                            {
                                AppDataLog.LogEvent(
                                    area: "Tracker",
                                    operation: "Sync.Retry",
                                    level: "warn",
                                    data: new System.Collections.Generic.Dictionary<string, string?>
                                    {
                                        ["item"] = GetItemName(item),
                                        ["retryCount"] = newRetry.ToString(),
                                        ["maxRetries"] = MaxRetries.ToString()
                                    });
                            }
                            catch
                            {
                            }

                            Debug.WriteLine($"[TrackerSync] ⚠️ Retry {newRetry}/{MaxRetries}: {GetItemName(item)}");
                            break; // Don't process more items if one failed
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerSync] Send error: {ex.Message}");

                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Sync.SendItem",
                            ex: ex,
                            data: new System.Collections.Generic.Dictionary<string, string?>
                            {
                                ["item"] = GetItemName(item)
                            });
                    }
                    catch
                    {
                    }
                    break;
                }
            }
        }

        private async Task<SendResult> SendItem(QueueItemWrapper item, CancellationToken ct)
        {
            string url;
            string json;

            if ((item.Type == "qc" || item.Type == "batch") && item.Batch is not null)
            {
                url = _apiBaseUrl + "/sync-qc";
                json = JsonSerializer.Serialize(item.Batch, JsonOptions);
            }
            else
            {
                Debug.WriteLine("[TrackerSync] Invalid queue item — no payload");
                return SendResult.Success; // Remove it
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(HttpTimeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrWhiteSpace(_trackerSecret))
                {
                    request.Headers.Remove("tracker-secret");
                    request.Headers.TryAddWithoutValidation("tracker-secret", _trackerSecret);
                }

                using var response = await _httpClient.SendAsync(request, cts.Token)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.Created)
                {
                    try
                    {
                        _queue.AppendHistory(new
                        {
                            ts = DateTimeOffset.Now.ToString("o"),
                            item = GetItemName(item),
                            outcome = "success",
                            status = (int)response.StatusCode,
                            payload = json
                        });
                    }
                    catch
                    {
                    }

                    return SendResult.Success;
                }

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                Debug.WriteLine($"[TrackerSync] HTTP {(int)response.StatusCode}: {body}");

                try
                {
                    var bodyPreview = body ?? string.Empty;
                    if (bodyPreview.Length > 600) bodyPreview = bodyPreview[..600];

                    _queue.AppendHistory(new
                    {
                        ts = DateTimeOffset.Now.ToString("o"),
                        item = GetItemName(item),
                        outcome = "http_error",
                        status = (int)response.StatusCode,
                        body = bodyPreview,
                        payload = json
                    });
                }
                catch
                {
                }

                // Permanent client-side errors should not block the entire queue.
                // Retry is appropriate for timeouts and rate limits.
                var statusInt = (int)response.StatusCode;
                if (statusInt >= 400 && statusInt < 500
                    && response.StatusCode != HttpStatusCode.Unauthorized
                    && response.StatusCode != HttpStatusCode.Forbidden
                    && response.StatusCode != HttpStatusCode.RequestTimeout
                    && response.StatusCode != (HttpStatusCode)429)
                {
                    try
                    {
                        AppDataLog.LogEvent(
                            area: "Tracker",
                            operation: "Sync.PermanentHttpError",
                            level: "error",
                            data: new System.Collections.Generic.Dictionary<string, string?>
                            {
                                ["url"] = url,
                                ["status"] = statusInt.ToString(),
                                ["body"] = body,
                                ["item"] = GetItemName(item)
                            });
                    }
                    catch
                    {
                    }

                    return SendResult.PermanentFailure;
                }

                try
                {
                    AppDataLog.LogEvent(
                        area: "Tracker",
                        operation: "Sync.HttpError",
                        level: "warn",
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["url"] = url,
                            ["status"] = ((int)response.StatusCode).ToString(),
                            ["body"] = body,
                            ["item"] = GetItemName(item)
                        });
                }
                catch
                {
                }
                return SendResult.RetryableFailure;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.WriteLine("[TrackerSync] Request timed out");

                try
                {
                    _queue.AppendHistory(new
                    {
                        ts = DateTimeOffset.Now.ToString("o"),
                        item = GetItemName(item),
                        outcome = "timeout",
                        payload = json
                    });
                }
                catch
                {
                }

                try
                {
                    AppDataLog.LogEvent(
                        area: "Tracker",
                        operation: "Sync.Timeout",
                        level: "warn",
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["url"] = url,
                            ["item"] = GetItemName(item)
                        });
                }
                catch
                {
                }
                return SendResult.RetryableFailure;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[TrackerSync] Network error: {ex.Message}");

                try
                {
                    _queue.AppendHistory(new
                    {
                        ts = DateTimeOffset.Now.ToString("o"),
                        item = GetItemName(item),
                        outcome = "network_error",
                        error = ex.Message,
                        payload = json
                    });
                }
                catch
                {
                }

                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Sync.NetworkError",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["url"] = url,
                            ["item"] = GetItemName(item)
                        });
                }
                catch
                {
                }
                return SendResult.RetryableFailure;
            }
        }

        private static string GetItemName(QueueItemWrapper item)
        {
            if ((item.Type == "qc" || item.Type == "batch") && item.Batch is not null)
                return $"qc({item.Batch.Files?.Count ?? 0} files) [{item.Batch.FileStatus}]";

            return "qc(?)";
        }
    }
}
