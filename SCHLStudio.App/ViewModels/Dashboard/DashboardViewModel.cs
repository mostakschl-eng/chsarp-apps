using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;
using SCHLStudio.App.ViewModels.LiveTracking.Services;
using SCHLStudio.App.ViewModels.LiveTracking.Tabs;
using SCHLStudio.App;

namespace SCHLStudio.App.ViewModels.Windows
{
    /// <summary>
    /// DashboardViewModel - Matches Python ui/components/dashboard_window.py
    /// Displays daily work statistics
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _statusText = "Loading...";
        private int _totalFiles;
        private string _totalWorkTime = "00:00:00";
        private string _totalPauseTime = "00:00:00";
        private string _avgTime = "00:00:00";
        private ObservableCollection<DashboardClientRow> _clientRows = [];

        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                    OnPropertyChanged(nameof(TitleText));
            }
        }

        public string TitleText
        {
            get
            {
                var displayName = !string.IsNullOrEmpty(Username)
                    ? char.ToUpper(Username[0]) + Username.Substring(1)
                    : "";
                return $"TODAY SUMMARY - {displayName}";
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set
            {
                if (SetProperty(ref _totalFiles, value))
                    OnPropertyChanged(nameof(TotalFilesText));
            }
        }

        public string TotalFilesText => $"FILES: {TotalFiles}";

        public string TotalWorkTime
        {
            get => _totalWorkTime;
            set
            {
                if (SetProperty(ref _totalWorkTime, value))
                    OnPropertyChanged(nameof(TotalWorkTimeText));
            }
        }

        public string TotalWorkTimeText => $"WORK TIME: {TotalWorkTime}";

        public string TotalPauseTime
        {
            get => _totalPauseTime;
            set
            {
                if (SetProperty(ref _totalPauseTime, value))
                    OnPropertyChanged(nameof(TotalPauseTimeText));
            }
        }

        public string TotalPauseTimeText => $"PAUSE TIME: {TotalPauseTime}";

        public string AvgTime
        {
            get => _avgTime;
            set
            {
                if (SetProperty(ref _avgTime, value))
                    OnPropertyChanged(nameof(AvgTimeText));
            }
        }

        public string AvgTimeText => $"AVG TIME: {AvgTime}";

        public ObservableCollection<DashboardClientRow> ClientRows
        {
            get => _clientRows;
            set => SetProperty(ref _clientRows, value);
        }

        public DashboardViewModel(string username)
        {
            Username = username;
        }

        public void LoadData(DashboardData data)
        {
            if (data == null)
            {
                StatusText = "No data available";
                return;
            }

            // Update totals
            TotalFiles = data.Totals.TotalFiles;
            TotalWorkTime = FormatTime(data.Totals.TotalWorkSeconds);
            TotalPauseTime = FormatTime(data.Totals.TotalPauseSeconds);
            AvgTime = FormatTime(data.Totals.AvgSeconds);

            // Update client rows
            ClientRows.Clear();
            var sortedClients = data.ByClient
                .OrderByDescending(kv => kv.Value.WorkSeconds)
                .ThenBy(kv => kv.Key);

            foreach (var (clientName, clientData) in sortedClients)
            {
                var displayWorkType = clientData.LastWorkType ?? "";

                // Strip parenthetical text from client name (matches Python behavior)
                var displayName = clientName;
                try
                {
                    if (!string.IsNullOrWhiteSpace(displayName) && displayName.Contains("|||"))
                    {
                        var parts = displayName.Split(["|||"], StringSplitOptions.None);
                        if (parts.Length >= 1)
                        {
                            displayName = parts[0];
                        }
                        if (parts.Length >= 2)
                        {
                            displayWorkType = parts[1];
                        }
                    }
                }
                catch
                {
                }
                if (displayName.EndsWith(")") && displayName.Contains(" ("))
                {
                    displayName = displayName.Split(" (")[0].Trim();
                }

                displayName = (displayName ?? string.Empty).Trim().ToUpperInvariant();

                ClientRows.Add(new DashboardClientRow
                {
                    ClientName = displayName,
                    Files = clientData.TotalFiles,
                    WorkTime = FormatTime(clientData.WorkSeconds),
                    PauseTime = FormatTime(clientData.PauseSeconds),
                    AvgTime = FormatTime(clientData.AvgSeconds),
                    WorkType = NormalizeWorkType(displayWorkType),
                    Category = clientData.LastCategory ?? ""
                });
            }

            StatusText = "Ready";
        }

        private string FormatTime(int seconds)
        {
            var hours = seconds / 3600;
            var minutes = (seconds % 3600) / 60;
            var secs = seconds % 60;
            return $"{hours:D2}:{minutes:D2}:{secs:D2}";
        }

        private string NormalizeWorkType(string? workType)
        {
            try
            {
                var wt = (workType ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(wt)) return string.Empty;

                if (wt.StartsWith("qc", StringComparison.OrdinalIgnoreCase))
                {
                    return "QC" + wt.Substring(2);
                }

                return wt.ToLowerInvariant();
            }
            catch
            {
                return (workType ?? string.Empty).Trim();
            }
        }
    }

    public sealed class DashboardLiveTrackingViewModel : ViewModelBase
    {
        public DashboardProductionTabViewModel ProductionTab { get; }
        public PauseTabViewModel PauseTab { get; }

        private readonly IApiClient _apiClient;
        private readonly ILiveTrackingDataService _liveTrackingDataService;
        private string _username = string.Empty;

        public string Username
        {
            get => _username;
            private set => SetProperty(ref _username, value);
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    _ = ReloadAsync();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusText = "Click Reload";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public RelayCommand ReloadCommand { get; }

        public DashboardLiveTrackingViewModel()
        {
            _apiClient = ((App)System.Windows.Application.Current)
                .ServiceProvider
                .GetRequiredService<IApiClient>();

            var httpClient = ((App)System.Windows.Application.Current)
                .ServiceProvider
                .GetRequiredService<System.Net.Http.HttpClient>();
            _liveTrackingDataService = new LiveTrackingDataService(httpClient);

            ProductionTab = new DashboardProductionTabViewModel();
            PauseTab = new PauseTabViewModel();

            ReloadCommand = new RelayCommand(_ => { _ = ReloadAsync(); });
        }

        public void SetUsername(string? username)
        {
            var normalized = (username ?? string.Empty).Trim();
            var changed = !string.Equals(Username, normalized, StringComparison.OrdinalIgnoreCase);
            Username = normalized;
            if (changed)
            {
                _ = ReloadAsync();
            }
        }

        private async Task ReloadAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Loading...";

                var date = SelectedDate.ToString("yyyy-MM-dd");
                var json = await _apiClient
                    .GetDashboardTodayAsync(Username, date)
                    .ConfigureAwait(false);

                var api = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonSerializer.Deserialize<DashboardTodayApiResponse>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                        }
                    );

                if (api == null || !api.Success)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ProductionTab.RefreshData(new List<LiveTrackingSessionModel>());
                        PauseTab.ResetSessionSnapshot();
                        PauseTab.RefreshData(new List<LiveTrackingSessionModel>(), true);
                        StatusText = "No data";
                    });
                    return;
                }

                var workLogs = MapDashboardWorkLogs(api?.WorkLogs);
                var sessionRows = (api?.Sessions ?? new List<DashboardSessionDto>())
                    .Where(s => s != null)
                    .ToList();

                // IMPORTANT:
                // Dashboard must NEVER fall back to Live Tracking snapshot data because Live Tracking
                // intentionally returns ALL users (no username filter). If the current user has no
                // worklogs for the day, dashboard should show empty rather than other users.

                var loginUsername = (Username ?? string.Empty).Trim();
                var appLoginUsername = (SCHLStudio.App.Configuration.AppConfig.CurrentAppUser ?? string.Empty).Trim();
                var displayName = (SCHLStudio.App.Configuration.AppConfig.CurrentDisplayName ?? string.Empty).Trim();

                // IMPORTANT:
                // Only apply identity filtering when the caller explicitly requested a username.
                // If Username is empty, dashboard is in "All" mode and must not be filtered down to
                // the current app login identity.
                var hasExplicitUserFilter = !string.IsNullOrWhiteSpace(loginUsername);

                var rawCandidates = new List<string>();
                if (hasExplicitUserFilter)
                {
                    rawCandidates.Add(loginUsername);
                    if (!string.IsNullOrWhiteSpace(appLoginUsername)) rawCandidates.Add(appLoginUsername);
                    if (!string.IsNullOrWhiteSpace(displayName)) rawCandidates.Add(displayName);
                }
                var identityAliases = hasExplicitUserFilter
                    ? BuildIdentityAliases(rawCandidates)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var filteredWorkLogsByUser = workLogs;
                if (identityAliases.Count > 0)
                {
                    filteredWorkLogsByUser = workLogs
                        .Where(w => w != null
                            && !string.IsNullOrWhiteSpace(w.EmployeeName)
                            && IdentityMatches(w.EmployeeName, identityAliases))
                        .ToList();
                }

                // If identity filtering is enabled, never fall back to unfiltered worklogs.
                // A mismatch should result in empty worklogs, not other users.
                if (identityAliases.Count > 0)
                {
                    workLogs = filteredWorkLogsByUser;
                }

                var filteredSessionsByUser = sessionRows;
                if (identityAliases.Count > 0)
                {
                    filteredSessionsByUser = sessionRows
                        .Where(s => s != null
                            && !string.IsNullOrWhiteSpace(s.Username)
                            && IdentityMatches(s.Username, identityAliases))
                        .ToList();
                }

                // If identity filtering is enabled, never fall back to unfiltered sessions.
                // A mismatch should result in empty sessions, not other users.
                if (identityAliases.Count > 0)
                {
                    sessionRows = filteredSessionsByUser;
                }

                var worklogNames = workLogs
                    .Where(w => w != null && !string.IsNullOrWhiteSpace(w.EmployeeName))
                    .Select(w => (w.EmployeeName ?? string.Empty).Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (worklogNames.Count > 0 && sessionRows.Count > 0)
                {
                    for (int i = 0; i < sessionRows.Count; i++)
                    {
                        var row = sessionRows[i];
                        if (string.IsNullOrWhiteSpace(row.Username)) continue;

                        var resolved = ResolveMatchingName(row.Username, worklogNames);
                        if (!string.IsNullOrWhiteSpace(resolved)
                            && !string.Equals(row.Username, resolved, StringComparison.OrdinalIgnoreCase))
                        {
                            row.Username = resolved;
                        }
                    }
                }

                var sessions = MapDashboardSessions(sessionRows);

                var activeSessionRows = sessionRows
                    .Where(s => s != null
                        && s.IsActive
                        && !string.IsNullOrWhiteSpace(s.Username))
                    .ToList();

                var activeUsers = activeSessionRows
                    .Select(s => (s.Username ?? string.Empty).Trim())
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Date filter safe behavior:
                // Use users present in session list for the selected date (not only currently active),
                // so past dates and same-day logout users still show data.
                var sessionUsers = sessions
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Username))
                    .Select(s => s.Username.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Fallback: if backend returns no session rows for that date, keep worklog users visible.
                if (sessionUsers.Count == 0)
                {
                    sessionUsers = workLogs
                        .Where(w => w != null && !string.IsNullOrWhiteSpace(w.EmployeeName))
                        .Select(w => w.EmployeeName.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                var filteredWorkLogs = workLogs
                    .Where(w => w != null
                        && !string.IsNullOrWhiteSpace(w.EmployeeName)
                        && sessionUsers.Contains(w.EmployeeName.Trim()))
                    .ToList();

                if (filteredWorkLogs.Count == 0 && workLogs.Count > 0 && sessionRows.Count > 0)
                {
                    var sessionAliases = BuildIdentityAliases(
                        sessionRows
                            .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Username))
                            .Select(s => s.Username ?? string.Empty)
                            .Where(u => !string.IsNullOrWhiteSpace(u))
                    );

                    if (sessionAliases.Count > 0)
                    {
                        filteredWorkLogs = workLogs
                            .Where(w => w != null
                                && !string.IsNullOrWhiteSpace(w.EmployeeName)
                                && IdentityMatches(w.EmployeeName, sessionAliases))
                            .ToList();
                    }
                }

                // Do NOT fall back to showing all worklogs.
                // If the current user's worklogs are empty, keep it empty.

                var activeWorkLogs = filteredWorkLogs
                    .Where(w => w != null
                        && !string.IsNullOrWhiteSpace(w.EmployeeName)
                        && activeUsers.Contains(w.EmployeeName.Trim()))
                    .ToList();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PauseTab.ResetSessionSnapshot();
                    foreach (var s in sessionRows)
                    {
                        if (s == null) continue;
                        PauseTab.ApplySessionUpdate(
                            s.Username ?? string.Empty,
                            s.FirstLoginAt,
                            s.IsActive ? null : s.LastLogoutAt,
                            s.TotalDurationSeconds
                        );
                    }

                    ProductionTab.RefreshData(filteredWorkLogs);

                    // Activity tab should still work even if the backend returns no session rows
                    // (some environments/users may not have sessions populated yet).
                    // PauseTabViewModel can infer active users by file status, but only if we provide
                    // the full set of sessions to evaluate.
                    var activitySource = filteredWorkLogs ?? new List<LiveTrackingSessionModel>();
                    var includeInactiveFromSessionSnapshot = activeSessionRows.Count == 0;

                    if (activitySource.Count == 0 && activeSessionRows.Count > 0)
                    {
                        activitySource = activeSessionRows
                            .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Username))
                            .Select(s => new LiveTrackingSessionModel
                            {
                                Id = $"session::{s.Username}",
                                EmployeeName = s.Username ?? string.Empty,
                                CreatedAt = s.FirstLoginAt ?? DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                Files = new System.Collections.ObjectModel.ObservableCollection<LiveTrackingFileModel>(),
                            })
                            .ToList();

                        // In this mode we only have session snapshot, not worklogs.
                        // Allow PauseTab to show these users.
                        includeInactiveFromSessionSnapshot = true;
                    }
                    // Dashboard always shows ALL logged-in users (not just working/paused).
                    // Unlike Live Tracking, the dashboard is a full day summary.
                    PauseTab.RefreshData(activitySource, true);

                    var sessionDurationByUser = sessionRows
                        .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Username) && s.FirstLoginAt.HasValue)
                        .GroupBy(s => (s?.Username ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            g => g.Key,
                            g =>
                            {
                                var backendSeconds = g
                                    .Where(x => x.TotalDurationSeconds > 0)
                                    .Sum(x => x.TotalDurationSeconds);
                                if (backendSeconds > 0)
                                {
                                    return backendSeconds / 60.0;
                                }

                                var first = g
                                    .Where(x => x.FirstLoginAt.HasValue)
                                    .Select(x => x.FirstLoginAt!.Value)
                                    .OrderBy(x => x)
                                    .FirstOrDefault();

                                var hasActive = g.Any(x => x.IsActive);
                                var last = hasActive
                                    ? DateTime.UtcNow
                                    : g.Where(x => x.LastLogoutAt.HasValue)
                                        .Select(x => x.LastLogoutAt!.Value)
                                        .OrderByDescending(x => x)
                                        .FirstOrDefault();

                                if (first == default)
                                {
                                    return 0d;
                                }

                                if (last == default)
                                {
                                    last = DateTime.UtcNow;
                                }

                                var minutes = (last - first).TotalMinutes;
                                return minutes > 0 ? minutes : 0d;
                            },
                            StringComparer.OrdinalIgnoreCase
                        );

                    foreach (var g in PauseTab.PauseGroups)
                    {
                        if (g == null) continue;

                        var key = (g.EmployeeName ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(key)
                            && sessionDurationByUser.TryGetValue(key, out var sessionDuration)
                            && sessionDuration > 0)
                        {
                            g.TotalDurationToday = sessionDuration;
                            var idleBySession = sessionDuration - (g.TotalWorkTime + g.TotalPauseTime);
                            g.IdleTime = idleBySession < 0 ? 0 : idleBySession;
                            continue;
                        }

                        if (g.TotalDurationToday <= 0 && g.FirstLogin.HasValue)
                        {
                            var start = g.FirstLogin.Value;
                            var end = g.LastLogout ?? DateTime.Now;
                            if (end >= start)
                            {
                                var duration = (end - start).TotalMinutes;
                                g.TotalDurationToday = duration;
                                var idle = duration - (g.TotalWorkTime + g.TotalPauseTime);
                                g.IdleTime = idle < 0 ? 0 : idle;
                            }
                        }
                    }

                    foreach (var g in PauseTab.PauseGroups)
                    {
                        if (g != null)
                        {
                            g.IsExpanded = true;
                        }
                    }

                    StatusText = ((filteredWorkLogs?.Count ?? 0) == 0 && (sessionRows?.Count ?? 0) == 0)
                        ? "No data"
                        : "Ready";
                });
            }
            catch (Exception ex)
            {
                try
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StatusText = $"Error: {ex.Message}";
                    });
                }
                catch
                {
                }
            }
            finally
            {
                try
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsLoading = false;
                    });
                }
                catch
                {
                    IsLoading = false;
                }
            }
        }

        private static HashSet<string> BuildIdentityAliases(IEnumerable<string> identities)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var identity in identities)
            {
                foreach (var alias in ExpandIdentityAliases(identity))
                {
                    aliases.Add(alias);
                }
            }

            return aliases;
        }

        private static bool IdentityMatches(string? candidate, HashSet<string> aliases)
        {
            if (aliases == null || aliases.Count == 0) return true;
            if (string.IsNullOrWhiteSpace(candidate)) return false;

            foreach (var item in ExpandIdentityAliases(candidate))
            {
                if (aliases.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> ExpandIdentityAliases(string? identity)
        {
            var raw = (identity ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            var canonicalRaw = CanonicalIdentity(raw);
            if (!string.IsNullOrWhiteSpace(canonicalRaw))
            {
                yield return canonicalRaw;
            }

            var noParentheses = Regex.Replace(raw, @"\s*\(.*?\)\s*", " ").Trim();
            var canonicalNoParentheses = CanonicalIdentity(noParentheses);
            if (!string.IsNullOrWhiteSpace(canonicalNoParentheses))
            {
                yield return canonicalNoParentheses;
            }

            var splitIndex = raw.IndexOf('-');
            if (splitIndex > 0 && splitIndex < raw.Length - 1)
            {
                var left = raw.Substring(0, splitIndex).Trim();
                var right = raw.Substring(splitIndex + 1).Trim();

                var canonicalLeft = CanonicalIdentity(left);
                var canonicalRight = CanonicalIdentity(right);

                if (!string.IsNullOrWhiteSpace(canonicalLeft))
                {
                    yield return canonicalLeft;
                }

                if (!string.IsNullOrWhiteSpace(canonicalRight))
                {
                    yield return canonicalRight;
                }

                if (IsMostlyDigits(canonicalLeft) && !string.IsNullOrWhiteSpace(canonicalRight))
                {
                    yield return canonicalRight;
                }
            }
        }

        private static string? ResolveMatchingName(string sourceName, List<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var sourceAliases = BuildIdentityAliases(new[] { sourceName });
            if (sourceAliases.Count == 0) return null;

            foreach (var candidate in candidates)
            {
                if (IdentityMatches(candidate, sourceAliases))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string CanonicalIdentity(string value)
        {
            var cleaned = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(cleaned)) return string.Empty;

            cleaned = cleaned.Replace("_", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private static bool IsMostlyDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            var digits = 0;
            var letters = 0;
            foreach (var ch in value)
            {
                if (char.IsDigit(ch)) digits++;
                else if (char.IsLetter(ch)) letters++;
            }

            return digits > 0 && letters == 0;
        }

        private static List<LiveTrackingSessionModel> MapDashboardWorkLogs(
            List<DashboardWorkLogDto>? rows
        )
        {
            var list = new List<LiveTrackingSessionModel>();
            if (rows == null) return list;

            static double SecondsToMinutes(double seconds)
            {
                if (seconds <= 0) return 0;
                return seconds / 60.0;
            }

            foreach (var dto in rows)
            {
                if (dto == null) continue;

                var model = new LiveTrackingSessionModel
                {
                    Id = dto.Id ?? Guid.NewGuid().ToString(),
                    EmployeeName = dto.EmployeeName ?? string.Empty,
                    Shift = dto.Shift ?? string.Empty,
                    FolderPath = dto.FolderPath ?? string.Empty,
                    WorkType = dto.WorkType ?? string.Empty,
                    ClientCode = dto.ClientCode ?? string.Empty,
                    Categories = dto.Categories ?? string.Empty,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt,
                    EstimateTime = dto.EstimateTime,
                    TotalTimes = SecondsToMinutes(dto.TotalTimes),
                    PauseCount = dto.PauseCount,
                    PauseTime = SecondsToMinutes(dto.PauseTime),
                    PauseReasons = dto.PauseReasons
                        ?.Select(x => (x?.Reason ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList()
                        ?? new List<string>(),
                    Files = new ObservableCollection<LiveTrackingFileModel>(),
                };

                if (dto.Files != null)
                {
                    foreach (var f in dto.Files
                        .OrderBy(x => string.Equals(x.FileStatus, "working", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                        .ThenByDescending(x => x.StartedAt ?? x.CompletedAt ?? DateTime.MinValue))
                    {
                        model.Files.Add(new LiveTrackingFileModel
                        {
                            FileName = f.FileName ?? string.Empty,
                            FileStatus = f.FileStatus ?? string.Empty,
                            Report = f.Report ?? string.Empty,
                            TimeSpent = SecondsToMinutes(f.TimeSpent),
                            StartTime = f.StartedAt,
                            EndTime = f.CompletedAt,
                        });
                    }
                }

                list.Add(model);
            }

            return list;
        }

        private static List<TrackerUserSessionModel> MapDashboardSessions(
            List<DashboardSessionDto>? rows
        )
        {
            var list = new List<TrackerUserSessionModel>();
            if (rows == null) return list;

            foreach (var s in rows)
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
                });
            }

            return list;
        }
    }

    public sealed class DashboardTodayApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("workLogs")]
        public List<DashboardWorkLogDto>? WorkLogs { get; set; }

        [JsonPropertyName("sessions")]
        public List<DashboardSessionDto>? Sessions { get; set; }
    }

    public sealed class DashboardSessionDto
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

    public sealed class DashboardWorkLogDto
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
        public List<DashboardPauseReasonDto>? PauseReasons { get; set; }

        [JsonPropertyName("files")]
        public List<DashboardWorkLogFileDto>? Files { get; set; }
    }

    public sealed class DashboardPauseReasonDto
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public sealed class DashboardWorkLogFileDto
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

    public sealed class DashboardProductionTabViewModel : ViewModelBase
    {
        private readonly ObservableCollection<LiveTrackingSessionModel> _productionRows = new();
        public ReadOnlyObservableCollection<LiveTrackingSessionModel> ProductionRows { get; }

        private int _activeUsers;
        public int ActiveUsers { get => _activeUsers; private set => SetProperty(ref _activeUsers, value); }

        private int _totalFiles;
        public int TotalFiles { get => _totalFiles; private set => SetProperty(ref _totalFiles, value); }

        private int _completedFiles;
        public int CompletedFiles { get => _completedFiles; private set => SetProperty(ref _completedFiles, value); }

        private string _avgTimePerFile = "0m";
        public string AvgTimePerFile { get => _avgTimePerFile; private set => SetProperty(ref _avgTimePerFile, value); }

        public DashboardProductionTabViewModel()
        {
            ProductionRows = new ReadOnlyObservableCollection<LiveTrackingSessionModel>(_productionRows);
        }

        public void RefreshData(System.Collections.Generic.List<LiveTrackingSessionModel> sessions)
        {
            try
            {
                if (sessions == null) return;

                var rows = sessions
                    .Where(s => s != null)
                    .OrderByDescending(s => s.UpdatedAt)
                    .ToList();

                ActiveUsers = rows
                    .Select(s => s.EmployeeName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                TotalFiles = rows.SelectMany(s => s.Files ?? []).Count();
                CompletedFiles = rows
                    .SelectMany(s => s.Files ?? [])
                    .Count(f => string.Equals(f.FileStatus, "done", StringComparison.OrdinalIgnoreCase));

                var totalTime = rows.Sum(s => s.TotalTimes);
                AvgTimePerFile = TotalFiles > 0
                    ? LiveTrackingFileModel.FormatMinutes(totalTime / TotalFiles)
                    : "0m";

                var newKeys = rows.Select(r => r.Id).ToHashSet();
                for (int i = _productionRows.Count - 1; i >= 0; i--)
                {
                    if (!newKeys.Contains(_productionRows[i].Id))
                    {
                        _productionRows.RemoveAt(i);
                    }
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    var incoming = rows[i];
                    var existing = _productionRows.FirstOrDefault(r => r.Id == incoming.Id);

                    if (existing == null)
                    {
                        _productionRows.Insert(i, incoming);
                    }
                    else
                    {
                        var currentIndex = _productionRows.IndexOf(existing);
                        incoming.IsExpanded = existing.IsExpanded;
                        _productionRows[currentIndex] = incoming;
                        if (currentIndex != i)
                        {
                            _productionRows.Move(currentIndex, i);
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Represents a single row in the dashboard client list
    /// </summary>
    public class DashboardClientRow : ViewModelBase
    {
        private string _clientName = string.Empty;
        private int _files;
        private string _workTime = "00:00:00";
        private string _pauseTime = "00:00:00";
        private string _avgTime = "00:00:00";
        private string _workType = string.Empty;
        private string _category = string.Empty;

        public string ClientName
        {
            get => _clientName;
            set => SetProperty(ref _clientName, value);
        }

        public int Files
        {
            get => _files;
            set => SetProperty(ref _files, value);
        }

        public string WorkTime
        {
            get => _workTime;
            set => SetProperty(ref _workTime, value);
        }

        public string PauseTime
        {
            get => _pauseTime;
            set => SetProperty(ref _pauseTime, value);
        }

        public string AvgTime
        {
            get => _avgTime;
            set => SetProperty(ref _avgTime, value);
        }

        public string WorkType
        {
            get => _workType;
            set => SetProperty(ref _workType, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }
    }

    /// <summary>
    /// Dashboard data structure matching Python cache format
    /// </summary>
    public class DashboardData
    {
        public DashboardTotals Totals { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, DashboardClientData> ByClient { get; set; } = new();
    }

    public class DashboardTotals
    {
        public int TotalFiles { get; set; }
        public int TotalWorkSeconds { get; set; }
        public int TotalPauseSeconds { get; set; }
        public int AvgSeconds { get; set; }
    }

    public class DashboardClientData
    {
        public int TotalFiles { get; set; }
        public int WorkSeconds { get; set; }
        public int PauseSeconds { get; set; }
        public int AvgSeconds { get; set; }
        public string? LastWorkType { get; set; }
        public string? LastCategory { get; set; }
    }
}
