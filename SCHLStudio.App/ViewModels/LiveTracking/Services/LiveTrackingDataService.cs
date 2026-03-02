using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SCHLStudio.App.Configuration;
using SCHLStudio.App.ViewModels.LiveTracking.Models;
using SocketIOClient;

namespace SCHLStudio.App.ViewModels.LiveTracking.Services
{
    public interface ILiveTrackingDataService
    {
        Task<LiveTrackingSnapshot> GetLiveTrackingDataAsync(string dateToday);
        Task<LiveTrackingSnapshot> GetLiveTrackingDataRangeAsync(string dateFrom, string dateTo);
        Task StartRealTimeUpdatesAsync(Action<JsonElement> onTrackerUpdated, Action<JsonElement> onReportUpdated, Action<JsonElement> onSessionUpdated);
        void StopRealTimeUpdates();
    }

    public sealed class LiveTrackingSnapshot
    {
        public List<LiveTrackingSessionModel> WorkLogs { get; set; } = new List<LiveTrackingSessionModel>();
        public List<TrackerUserSessionModel> Sessions { get; set; } = new List<TrackerUserSessionModel>();
    }

    public sealed class TrackerUserSessionModel
    {
        public string Username { get; set; } = string.Empty;
        public DateTime? FirstLoginAt { get; set; }
        public DateTime? LastLogoutAt { get; set; }
        public bool IsActive { get; set; }
        public double TotalDurationSeconds { get; set; }
    }

    public class LiveTrackingDataService : ILiveTrackingDataService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private SocketIOClient.SocketIO? _socket;

        private static double SecondsToMinutes(double seconds)
        {
            if (seconds <= 0) return 0;
            return seconds / 60.0;
        }

        public LiveTrackingDataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _baseUrl = (AppConfig.GetApiBaseUrl() ?? string.Empty).Trim();
        }

        public async Task<LiveTrackingSnapshot> GetLiveTrackingDataAsync(string dateToday)
        {
            try
            {
                var baseUrl = _baseUrl.TrimEnd('/');
                if (!baseUrl.EndsWith("/tracker", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl += "/tracker";
                }
                var url = $"{baseUrl}/live-tracking-data";
                
                var payload = new { dateToday };
                var json = JsonSerializer.Serialize(payload);
                using var request = BuildTrackerRequest(url, json);
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<LiveTrackingApiResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true && result.Data != null)
                {
                    return new LiveTrackingSnapshot
                    {
                        WorkLogs = MapToModels(result.Data),
                        Sessions = MapSessions(result.Sessions)
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiveTrackingDataService] GetLiveTrackingDataAsync error: {ex}");
            }

            return new LiveTrackingSnapshot();
        }

        public async Task<LiveTrackingSnapshot> GetLiveTrackingDataRangeAsync(string dateFrom, string dateTo)
        {
            try
            {
                var baseUrl = _baseUrl.TrimEnd('/');
                if (!baseUrl.EndsWith("/tracker", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl += "/tracker";
                }
                var url = $"{baseUrl}/live-tracking-data";

                var payload = new { dateFrom, dateTo };
                var json = JsonSerializer.Serialize(payload);
                using var request = BuildTrackerRequest(url, json);
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<LiveTrackingApiResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true && result.Data != null)
                {
                    return new LiveTrackingSnapshot
                    {
                        WorkLogs = MapToModels(result.Data),
                        Sessions = MapSessions(result.Sessions)
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiveTrackingDataService] GetLiveTrackingDataRangeAsync error: {ex}");
            }

            return new LiveTrackingSnapshot();
        }

        public async Task StartRealTimeUpdatesAsync(Action<JsonElement> onTrackerUpdated, Action<JsonElement> onReportUpdated, Action<JsonElement> onSessionUpdated)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_baseUrl))
                {
                    Debug.WriteLine("[LiveTrackingDataService] Missing ApiBaseUrl; cannot start Socket.IO updates.");
                    return;
                }

                if (_socket != null)
                {
                    await _socket.DisconnectAsync().ConfigureAwait(false);
                    _socket.Dispose();
                }

                // WebSocket gateway runs under the Socket.IO namespace `/tracker`.
                // Our configured API base URL may include `/v1/tracker`, so we must
                // derive the socket origin (scheme + host + port) and then append `/tracker`.
                var apiUri = new Uri(_baseUrl.TrimEnd('/'), UriKind.Absolute);
                var socketOrigin = apiUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
                var uri = new Uri(socketOrigin + "/tracker");
                _socket = new SocketIOClient.SocketIO(uri, new SocketIOClient.SocketIOOptions
                {
                    Path = "/socket.io",
                    EIO = SocketIO.Core.EngineIO.V4,
                    ReconnectionDelay = 5000,
                });

                _socket.OnConnected += (sender, e) =>
                {
                    Debug.WriteLine("[LiveTrackingDataService] Socket.IO Connected!");
                    try
                    {
                        var sessionId = AppConfig.CurrentTrackerSessionId;
                        var username = ResolveLiveTrackingUsername();
                        _ = _socket.EmitAsync("SUBSCRIBE_LIVE_TRACKING", new
                        {
                            sessionId,
                            username,
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LiveTrackingDataService] SUBSCRIBE_LIVE_TRACKING emit error: {ex}");
                    }
                };

                _socket.On("TRACKER_SUBSCRIBE_DENIED", async response =>
                {
                    Debug.WriteLine("[LiveTrackingDataService] Received TRACKER_SUBSCRIBE_DENIED. Stopping connection permanently.");
                    try
                    {
                        if (_socket != null)
                        {
                            await _socket.DisconnectAsync().ConfigureAwait(false);
                            _socket.Dispose();
                            _socket = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LiveTrackingDataService] Error disconnecting on denied event: {ex}");
                    }
                });

                _socket.OnDisconnected += (sender, e) =>
                {
                    Debug.WriteLine("[LiveTrackingDataService] Socket.IO Disconnected");
                };

                // The backend emits TRACKER_UPDATED
                _socket.On("TRACKER_UPDATED", response =>
                {
                    try
                    {
                        var data = response.GetValue<JsonElement>();
                        onTrackerUpdated?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LiveTrackingDataService] TRACKER_UPDATED parsing error: {ex}");
                    }
                });

                // The backend emits TRACKER_REPORT_UPDATED
                _socket.On("TRACKER_REPORT_UPDATED", response =>
                {
                    try
                    {
                        var data = response.GetValue<JsonElement>();
                        onReportUpdated?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LiveTrackingDataService] TRACKER_REPORT_UPDATED parsing error: {ex}");
                    }
                });

                // The backend emits TRACKER_SESSION_UPDATED
                _socket.On("TRACKER_SESSION_UPDATED", response =>
                {
                    try
                    {
                        var data = response.GetValue<JsonElement>();
                        onSessionUpdated?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LiveTrackingDataService] TRACKER_SESSION_UPDATED parsing error: {ex}");
                    }
                });

                await _socket.ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiveTrackingDataService] StartRealTimeUpdatesAsync error: {ex}");
            }
        }

        private static string ResolveLiveTrackingUsername()
        {
            try
            {
                var display = (AppConfig.CurrentDisplayName ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(display))
                {
                    return display;
                }
            }
            catch
            {
            }

            try
            {
                return (AppConfig.CurrentAppUser ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public void StopRealTimeUpdates()
        {
            try
            {
                if (_socket != null)
                {
                    try
                    {
                        _ = _socket.DisconnectAsync();
                    }
                    catch
                    {
                    }

                    try
                    {
                        _socket.Dispose();
                    }
                    catch
                    {
                    }

                    _socket = null;
                }
            }
            catch { }
        }

        private static HttpRequestMessage BuildTrackerRequest(string url, string json)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var secret = AppConfig.GetTrackerSecret();
            if (!string.IsNullOrWhiteSpace(secret))
            {
                request.Headers.Remove("tracker-secret");
                request.Headers.TryAddWithoutValidation("tracker-secret", secret.Trim());
            }

            return request;
        }

        private List<LiveTrackingSessionModel> MapToModels(List<QcWorkLogDto> dtos)
        {
            var models = new List<LiveTrackingSessionModel>();
            foreach (var dto in dtos)
            {
                var model = new LiveTrackingSessionModel
                {
                    Id = dto.Id ?? Guid.NewGuid().ToString(),
                    EmployeeName = dto.EmployeeName ?? "",
                    Shift = dto.Shift ?? "",
                    FolderPath = dto.FolderPath ?? "",
                    WorkType = dto.WorkType ?? "",
                    ClientCode = dto.ClientCode ?? "",
                    Categories = dto.Categories ?? "",
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt,
                    // estimate_time is stored in minutes (from order ET), NOT seconds.
                    EstimateTime = dto.EstimateTime,
                    TotalTimes = SecondsToMinutes(dto.TotalTimes),
                    PauseCount = dto.PauseCount,
                    PauseTime = SecondsToMinutes(dto.PauseTime),
                    PauseReasons = dto.PauseReasons
                        ?.Select(x => (x?.Reason ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList()
                        ?? new List<string>(),
                    Files = new System.Collections.ObjectModel.ObservableCollection<LiveTrackingFileModel>()
                };

                if (dto.Files != null)
                {
                    foreach (var f in dto.Files
                        .OrderBy(x => string.Equals(x.FileStatus, "working", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                        .ThenByDescending(x => x.StartedAt ?? x.CompletedAt ?? DateTime.MinValue))
                    {
                        model.Files.Add(new LiveTrackingFileModel
                        {
                            FileName = f.FileName ?? "",
                            FileStatus = f.FileStatus ?? "",
                            Report = f.Report ?? "",
                            // Backend stores seconds; UI expects minutes.
                            TimeSpent = SecondsToMinutes(f.TimeSpent),
                            StartTime = f.StartedAt,
                            EndTime = f.CompletedAt
                        });
                    }
                }

                models.Add(model);
            }
            return models;
        }

        private class LiveTrackingApiResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public List<QcWorkLogDto>? Data { get; set; }

            [JsonPropertyName("sessions")]
            public List<UserSessionDto>? Sessions { get; set; }
        }

        private class UserSessionDto
        {
            [JsonPropertyName("username")]
            public string? Username { get; set; }

            [JsonPropertyName("first_login_at")]
            public DateTime? FirstLoginAt { get; set; }

            [JsonPropertyName("last_logout_at")]
            public DateTime? LastLogoutAt { get; set; }

            [JsonPropertyName("is_active")]
            public bool IsActive { get; set; }

            [JsonPropertyName("total_duration_seconds")]
            public double TotalDurationSeconds { get; set; }
        }

        private class QcWorkLogDto
        {
            [JsonPropertyName("_id")]
            public string? Id { get; set; }

            [JsonPropertyName("employee_name")]
            public string? EmployeeName { get; set; }

            [JsonPropertyName("shift")]
            public string? Shift { get; set; }

            [JsonPropertyName("folder_path")]
            public string? FolderPath { get; set; }

            [JsonPropertyName("work_type")]
            public string? WorkType { get; set; }

            [JsonPropertyName("client_code")]
            public string? ClientCode { get; set; }

            [JsonPropertyName("categories")]
            public string? Categories { get; set; }

            [JsonPropertyName("createdAt")]
            public DateTime CreatedAt { get; set; }

            [JsonPropertyName("updatedAt")]
            public DateTime UpdatedAt { get; set; }

            [JsonPropertyName("estimate_time")]
            public double EstimateTime { get; set; }

            [JsonPropertyName("total_times")]
            public double TotalTimes { get; set; }

            [JsonPropertyName("pause_count")]
            public int PauseCount { get; set; }

            [JsonPropertyName("pause_time")]
            public double PauseTime { get; set; }

            [JsonPropertyName("pause_reasons")]
            public List<PauseReasonDto>? PauseReasons { get; set; }

            [JsonPropertyName("files")]
            public List<QcWorkLogFileDto>? Files { get; set; }
        }

        private class PauseReasonDto
        {
            [JsonPropertyName("reason")]
            public string? Reason { get; set; }

            [JsonPropertyName("duration")]
            public double Duration { get; set; }

            [JsonPropertyName("started_at")]
            public DateTime? StartedAt { get; set; }

            [JsonPropertyName("completed_at")]
            public DateTime? CompletedAt { get; set; }
        }

        private class QcWorkLogFileDto
        {
            [JsonPropertyName("file_name")]
            public string? FileName { get; set; }

            [JsonPropertyName("file_status")]
            public string? FileStatus { get; set; }

            [JsonPropertyName("report")]
            public string? Report { get; set; }

            [JsonPropertyName("time_spent")]
            public double TimeSpent { get; set; }

            [JsonPropertyName("started_at")]
            public DateTime? StartedAt { get; set; }

            [JsonPropertyName("completed_at")]
            public DateTime? CompletedAt { get; set; }
        }

        private static List<TrackerUserSessionModel> MapSessions(List<UserSessionDto>? dtos)
        {
            var list = new List<TrackerUserSessionModel>();
            if (dtos == null) return list;

            foreach (var s in dtos)
            {
                if (s == null) continue;
                var username = (s.Username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(username)) continue;

                list.Add(new TrackerUserSessionModel
                {
                    Username = username,
                    FirstLoginAt = s.FirstLoginAt,
                    LastLogoutAt = s.LastLogoutAt,
                    IsActive = s.IsActive,
                    TotalDurationSeconds = s.TotalDurationSeconds,
                });
            }

            return list;
        }
    }
}
