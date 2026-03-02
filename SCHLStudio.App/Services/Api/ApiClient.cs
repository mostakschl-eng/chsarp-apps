using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SCHLStudio.App.Services.Api.Tracker;

namespace SCHLStudio.App.Services.Api
{
    public sealed class ApiClient : IApiClient
    {
        private readonly AuthService _authService;
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private readonly object _syncLock = new();
        private TrackerSyncService? _trackerSync;

        public bool IsAuthenticated { get; private set; }

        public ApiClient(HttpClient httpClient, string apiBaseUrl)
        {
            _httpClient = httpClient;
            _apiBaseUrl = apiBaseUrl ?? string.Empty;
            _authService = new AuthService(httpClient, _apiBaseUrl);

            try
            {
                if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                {
                    var hasV1 = apiBaseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                        || apiBaseUrl.Contains("/v1/", StringComparison.OrdinalIgnoreCase);
                    if (!hasV1)
                    {
                        Debug.WriteLine("WARNING: API base url does not include '/v1' in the path");
                    }
                }
            }
            catch (Exception ex)
            {
                LogNonCritical("ApiClient.Ctor.ValidateBaseUrl", ex);
            }
        }

        public async Task<string> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var typed = await LoginTypedAsync(username, password, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(typed);
        }

        public async Task<ApiLoginResult> LoginTypedAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var result = await _authService.LoginTypedAsync(username, password, cancellationToken).ConfigureAwait(false);
            try
            {
                if (result.Success || result.Valid)
                {
                    IsAuthenticated = true;
                }
            }
            catch (Exception ex)
            {
                LogNonCritical("LoginAsync.ParseAuthResponse", ex);
            }

            return result;
        }

        public Task<string> SetPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            return SerializeTypedAsync(() => SetPasswordTypedAsync(username, password, cancellationToken));
        }

        public Task<ApiSetPasswordResult> SetPasswordTypedAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            return _authService.SetPasswordTypedAsync(username, password, cancellationToken);
        }

        public Task<string> CheckUserAsync(string username, CancellationToken cancellationToken = default)
        {
            return SerializeTypedAsync(() => CheckUserTypedAsync(username, cancellationToken));
        }

        public Task<ApiCheckUserResult> CheckUserTypedAsync(string username, CancellationToken cancellationToken = default)
        {
            return _authService.CheckUserTypedAsync(username, cancellationToken);
        }

        public Task<string> GetJobListAsync(string? clientCode = null, CancellationToken cancellationToken = default)
        {
            return _authService.GetJobListAsync(clientCode, cancellationToken);
        }

        public Task<string> SearchFileAsync(string query, string? clientCode = null, CancellationToken cancellationToken = default)
        {
            return SerializeTypedAsync(() => SearchFileTypedAsync(query, clientCode, cancellationToken));
        }

        public Task<ApiSearchFileResult> SearchFileTypedAsync(string query, string? clientCode = null, CancellationToken cancellationToken = default)
        {
            return _authService.SearchFileTypedAsync(query, clientCode, cancellationToken);
        }

        public Task<string> ReportFileAsync(ApiReportFileRequest request, CancellationToken cancellationToken = default)
        {
            return SerializeTypedAsync(() => ReportFileTypedAsync(request, cancellationToken));
        }

        public Task<bool> ReportFileTypedAsync(ApiReportFileRequest request, CancellationToken cancellationToken = default)
        {
            return _authService.ReportFileTypedAsync(request, cancellationToken);
        }

        public Task<string> GetDashboardTodayAsync(string? username = null, string? date = null, CancellationToken cancellationToken = default)
        {
            return _authService.GetDashboardTodayAsync(username, date, cancellationToken);
        }

        public Task<bool> LogoutAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                IsAuthenticated = false;
            }
            catch (Exception ex)
            {
                LogNonCritical("LogoutAsync.SetIsAuthenticated", ex);
            }

            return _authService.LogoutAsync(sessionId, cancellationToken);
        }

        public Task<bool> WaitForSyncAsync(int timeoutSeconds = 10, CancellationToken cancellationToken = default)
        {
            return WaitForTrackerDrainAsync(timeoutSeconds, cancellationToken);
        }

        public void Stop()
        {
            TrackerSyncService? tracker;
            lock (_syncLock)
            {
                tracker = _trackerSync;
                _trackerSync = null;
            }

            try
            {
                tracker?.Stop();
            }
            catch (Exception ex)
            {
                LogNonCritical("Stop.TrackerSync", ex);
            }
        }

        public void AttachTrackerSync(TrackerSyncService trackerSync)
        {
            if (trackerSync is null)
            {
                return;
            }

            lock (_syncLock)
            {
                _trackerSync = trackerSync;
            }
        }

        public TrackerSyncService? GetTrackerSync()
        {
            lock (_syncLock)
            {
                return _trackerSync;
            }
        }

        public TrackerSyncService? EnsureTrackerSync(string? userName)
        {
            lock (_syncLock)
            {
                if (_trackerSync is not null)
                {
                    return _trackerSync;
                }

                var baseUrl = (_apiBaseUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return null;
                }

                var trackerUrl = baseUrl.TrimEnd('/');
                if (!trackerUrl.EndsWith("/tracker", StringComparison.OrdinalIgnoreCase))
                {
                    trackerUrl += "/tracker";
                }

                _trackerSync = new TrackerSyncService(
                    _httpClient,
                    trackerUrl,
                    () => IsAuthenticated,
                    userName);

                try
                {
                    _trackerSync.Start();
                }
                catch (Exception ex)
                {
                    LogNonCritical("EnsureTrackerSync.Start", ex);
                }

                return _trackerSync;
            }
        }

        public void DetachTrackerSync(TrackerSyncService trackerSync)
        {
            if (trackerSync is null)
            {
                return;
            }

            lock (_syncLock)
            {
                if (ReferenceEquals(_trackerSync, trackerSync))
                {
                    _trackerSync = null;
                }
            }
        }

        private async Task<bool> WaitForTrackerDrainAsync(int timeoutSeconds, CancellationToken cancellationToken)
        {
            var timeout = timeoutSeconds <= 0
                ? TimeSpan.FromSeconds(10)
                : TimeSpan.FromSeconds(timeoutSeconds);

            var startedAt = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                TrackerSyncService? tracker;
                lock (_syncLock)
                {
                    tracker = _trackerSync;
                }

                if (tracker is null)
                {
                    return true;
                }

                var pending = 0;
                try
                {
                    pending = tracker.PendingCount;
                }
                catch (Exception ex)
                {
                    LogNonCritical("WaitForSyncAsync.PendingCount", ex);
                    return false;
                }

                if (pending <= 0)
                {
                    return true;
                }

                if ((DateTime.UtcNow - startedAt) >= timeout)
                {
                    return false;
                }

                try
                {
                    tracker.TriggerSync();
                }
                catch (Exception ex)
                {
                    LogNonCritical("WaitForSyncAsync.TriggerSync", ex);
                }

                try
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            return false;
        }

        private static void LogNonCritical(string operation, Exception ex)
        {
            try
            {
                Debug.WriteLine($"[ApiClient] {operation} non-critical: {ex.Message}");
            }
            catch
            {
            }
        }

        private static async Task<string> SerializeTypedAsync<T>(Func<Task<T>> action)
        {
            var typed = await action().ConfigureAwait(false);
            return JsonSerializer.Serialize(typed);
        }
    }
}
