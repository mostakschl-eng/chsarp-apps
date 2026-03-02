using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Services.Api.Tracker
{
    /// <summary>
    /// Thread-safe, file-backed JSON queue for work log entries.
    /// Survives app crashes — data is never lost.
    /// Uses atomic temp-file + rename pattern for crash safety.
    /// </summary>
    public sealed class TrackerLocalQueue
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private const int MaxQueueSize = 500;

        private readonly string _queueFilePath;
        private readonly string _failedFilePath;
        private readonly string _historyFilePath;
        private readonly object _lock = new();

        public TrackerLocalQueue(string queueFilePath)
        {
            _queueFilePath = queueFilePath;
            _failedFilePath = Path.Combine(Path.GetDirectoryName(_queueFilePath) ?? string.Empty, "failed_sync.jsonl");
            _historyFilePath = Path.Combine(Path.GetDirectoryName(_queueFilePath) ?? string.Empty, "worklog_history.jsonl");

            try
            {
                var dir = Path.GetDirectoryName(_queueFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerQueue] Failed to create queue directory: {ex.Message}");
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Queue.Init.CreateDirectory",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["queueFilePath"] = _queueFilePath
                        });
                }
                catch
                {
                }
            }
        }

        public void AppendHistory(object record)
        {
            lock (_lock)
            {
                try
                {
                    if (record is null)
                    {
                        return;
                    }

                    var json = JsonSerializer.Serialize(record, JsonOptions);
                    File.AppendAllText(_historyFilePath, json + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.History.Append",
                            ex: ex,
                            data: new Dictionary<string, string?>
                            {
                                ["historyFilePath"] = _historyFilePath
                            });
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Backup the item to failed_sync.jsonl and remove it from the queue.
        /// Use this when discarding items (max retries) so data is never lost.
        /// </summary>
        public void BackupToFailedAndRemoveAt(int index, string reason)
        {
            lock (_lock)
            {
                try
                {
                    var queue = ReadQueue();
                    if (index < 0 || index >= queue.Count) return;

                    var item = queue[index];
                    AppendFailedItem(item, reason);
                    queue.RemoveAt(index);
                    WriteQueue(queue);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] BackupToFailedAndRemoveAt failed: {ex.Message}");
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.BackupToFailedAndRemoveAt",
                            ex: ex,
                            data: new Dictionary<string, string?>
                            {
                                ["index"] = index.ToString(),
                                ["reason"] = reason
                            });
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Enqueue a batch work log entry (QC work types).
        /// </summary>
        public bool Enqueue(SyncQcWorkLogDto item)
        {
            try
            {
                // Assign a unique sync ID for idempotency (backend dedup)
                if (string.IsNullOrWhiteSpace(item.SyncId))
                {
                    item.SyncId = Guid.NewGuid().ToString("N");
                }

                var wrapper = new QueueItemWrapper
                {
                    Type = "qc",
                    Batch = item,
                    RetryCount = 0,
                    QueuedAt = DateTime.UtcNow.ToString("o")
                };

                return EnqueueInternal(wrapper);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerQueue] Enqueue batch failed: {ex.Message}");
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Queue.Enqueue.Qc",
                        ex: ex);
                }
                catch
                {
                }
                return false;
            }
        }

        /// <summary>
        /// Read all queued items without removing them.
        /// </summary>
        public List<QueueItemWrapper> PeekAll()
        {
            lock (_lock)
            {
                try
                {
                    return ReadQueue();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] PeekAll failed: {ex.Message}");
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.PeekAll",
                            ex: ex,
                            data: new Dictionary<string, string?>
                            {
                                ["queueFilePath"] = _queueFilePath
                            });
                    }
                    catch
                    {
                    }
                    return new List<QueueItemWrapper>();
                }
            }
        }

        /// <summary>
        /// Remove the first N items from the queue (after successful sync).
        /// </summary>
        public void RemoveFirst(int count)
        {
            if (count <= 0) return;

            lock (_lock)
            {
                try
                {
                    var queue = ReadQueue();
                    if (count >= queue.Count)
                    {
                        queue.Clear();
                    }
                    else
                    {
                        queue.RemoveRange(0, count);
                    }
                    WriteQueue(queue);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] RemoveFirst failed: {ex.Message}");
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.RemoveFirst",
                            ex: ex,
                            data: new Dictionary<string, string?>
                            {
                                ["count"] = count.ToString()
                            });
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Converts any pending "working" or "in_progress" items to "paused".
        /// Call this on app startup so a crashed session doesn't reboot and upload "working" statuses for dropped files.
        /// </summary>
        public void SanitizeLingeringWorkingFiles()
        {
            lock (_lock)
            {
                try
                {
                    var queue = ReadQueue();
                    var changed = false;

                    foreach (var item in queue)
                    {
                        if (item?.Batch != null)
                        {
                            var status = (item.Batch.FileStatus ?? string.Empty).Trim().ToLowerInvariant();
                            if (status == "working" || status == "in_progress" || status == "in progress")
                            {
                                item.Batch.FileStatus = "paused";
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        WriteQueue(queue);
                        Debug.WriteLine("[TrackerQueue] Sanitized lingering working statuses to paused.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] SanitizeLingeringWorkingFiles failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Remove a specific item by index.
        /// </summary>
        public void RemoveAt(int index)
        {
            lock (_lock)
            {
                try
                {
                    var queue = ReadQueue();
                    if (index >= 0 && index < queue.Count)
                    {
                        queue.RemoveAt(index);
                        WriteQueue(queue);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] RemoveAt failed: {ex.Message}");
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.RemoveAt",
                            ex: ex,
                            data: new Dictionary<string, string?>
                            {
                                ["index"] = index.ToString()
                            });
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Increment retry count for item at index. Returns new retry count.
        /// </summary>
        public int IncrementRetry(int index)
        {
            lock (_lock)
            {
                try
                {
                    var queue = ReadQueue();
                    if (index >= 0 && index < queue.Count)
                    {
                        queue[index].RetryCount++;
                        WriteQueue(queue);
                        return queue[index].RetryCount;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] IncrementRetry failed: {ex.Message}");
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.IncrementRetry",
                            ex: ex,
                            data: new Dictionary<string, string?>
                            {
                                ["index"] = index.ToString()
                            });
                    }
                    catch
                    {
                    }
                }
            }
            return -1;
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    try
                    {
                        return ReadQueue().Count;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
        }

        // ── Internal ──

        private bool EnqueueInternal(QueueItemWrapper wrapper)
        {
            lock (_lock)
            {
                try
                {
                    var queue = ReadQueue();

                    if (queue.Count >= MaxQueueSize)
                    {
                        try
                        {
                            // Overflow protection: move oldest to failed file before dropping it
                            AppendFailedItem(queue[0], reason: "queue_overflow");
                            queue.RemoveAt(0);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[TrackerQueue] Overflow handling failed: {ex.Message}");
                            try
                            {
                                AppDataLog.LogError(
                                    area: "Tracker",
                                    operation: "Queue.Overflow",
                                    ex: ex,
                                    data: new Dictionary<string, string?>
                                    {
                                        ["maxQueueSize"] = MaxQueueSize.ToString()
                                    });
                            }
                            catch
                            {
                            }
                        }
                    }

                    queue.Add(wrapper);
                    WriteQueue(queue);

                    var name = wrapper.Type == "qc" || wrapper.Type == "batch"
                        ? $"qc ({wrapper.Batch?.Files?.Count ?? 0} files)"
                        : "qc (?)";

                    Debug.WriteLine($"[TrackerQueue] Queued: {name} [{wrapper.Batch?.FileStatus}] (Queue size: {queue.Count})");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TrackerQueue] EnqueueInternal failed: {ex.Message}");
                    try
                    {
                        AppDataLog.LogError(
                            area: "Tracker",
                            operation: "Queue.EnqueueInternal",
                            ex: ex);
                    }
                    catch
                    {
                    }
                    return false;
                }
            }
        }

        private void AppendFailedItem(QueueItemWrapper wrapper, string reason)
        {
            try
            {
                var payload = new Dictionary<string, object?>
                {
                    ["ts"] = DateTime.UtcNow.ToString("O"),
                    ["reason"] = reason,
                    ["item"] = wrapper
                };

                var line = JsonSerializer.Serialize(payload, JsonOptions);
                File.AppendAllText(_failedFilePath, line + Environment.NewLine);

                try
                {
                    AppDataLog.LogEvent(
                        area: "Tracker",
                        operation: "Queue.FailedBackup",
                        level: "warn",
                        data: new Dictionary<string, string?>
                        {
                            ["reason"] = reason,
                            ["failedFile"] = _failedFilePath
                        });
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerQueue] AppendFailedItem failed: {ex.Message}");
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Queue.AppendFailedItem",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["reason"] = reason,
                            ["failedFile"] = _failedFilePath
                        });
                }
                catch
                {
                }
            }
        }

        private List<QueueItemWrapper> ReadQueue()
        {
            try
            {
                if (!File.Exists(_queueFilePath))
                    return new List<QueueItemWrapper>();

                var json = File.ReadAllText(_queueFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<QueueItemWrapper>();

                return JsonSerializer.Deserialize<List<QueueItemWrapper>>(json, JsonOptions)
                       ?? new List<QueueItemWrapper>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerQueue] ReadQueue failed: {ex.Message}");
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Queue.Read",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["queueFilePath"] = _queueFilePath
                        });
                }
                catch
                {
                }
                return new List<QueueItemWrapper>();
            }
        }

        private void WriteQueue(List<QueueItemWrapper> queue)
        {
            try
            {
                var json = JsonSerializer.Serialize(queue, JsonOptions);
                var tmpPath = _queueFilePath + ".tmp";

                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, _queueFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerQueue] WriteQueue failed: {ex.Message}");
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Queue.Write",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["queueFilePath"] = _queueFilePath,
                            ["count"] = queue?.Count.ToString() ?? "0"
                        });
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>
    /// Wrapper that holds either a per-file or batch work log in the queue.
    /// </summary>
    public sealed class QueueItemWrapper
    {
        public string Type { get; set; } = "qc";
        public SyncQcWorkLogDto? Batch { get; set; }
        public int RetryCount { get; set; }
        public string? QueuedAt { get; set; }
    }
}
