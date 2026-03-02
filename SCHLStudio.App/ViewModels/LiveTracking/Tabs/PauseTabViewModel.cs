using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;

namespace SCHLStudio.App.ViewModels.LiveTracking.Tabs
{
    public sealed class PauseTabViewModel : ViewModelBase
    {
        private readonly ObservableCollection<PauseUserGroupModel> _pauseGroups = new();
        public ReadOnlyObservableCollection<PauseUserGroupModel> PauseGroups { get; }

        private readonly Dictionary<string, (DateTime? LoginAt, DateTime? LogoutAt, double? TotalDurationMinutes)> _latestSessionsByUser = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (string Status, DateTime Timestamp)> _latestRealtimeStatusByUser = new(StringComparer.OrdinalIgnoreCase);

        private int _totalPausedUsers;
        public int TotalPausedUsers
        {
            get => _totalPausedUsers;
            private set => SetProperty(ref _totalPausedUsers, value);
        }

        private string _totalPauseTime = "0m";
        public string TotalPauseTimeFormatted
        {
            get => _totalPauseTime;
            private set => SetProperty(ref _totalPauseTime, value);
        }

        private string _avgPauseTime = "0m";
        public string AvgPauseTimeFormatted
        {
            get => _avgPauseTime;
            private set => SetProperty(ref _avgPauseTime, value);
        }

        private string _topPauseReason = "—";
        public string TopPauseReason
        {
            get => _topPauseReason;
            private set => SetProperty(ref _topPauseReason, value);
        }

        public PauseTabViewModel()
        {
            PauseGroups = new ReadOnlyObservableCollection<PauseUserGroupModel>(_pauseGroups);
        }

        public void ResetSessionSnapshot()
        {
            try
            {
                _latestSessionsByUser.Clear();
                _latestRealtimeStatusByUser.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PauseTabViewModel] ResetSessionSnapshot error: {ex.Message}");
            }
        }

        public void ApplyFileStatusUpdate(string username, string? fileStatus, DateTime? timestamp = null)
        {
            try
            {
                var key = (username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key)) return;

                var normalizedStatus = (fileStatus ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedStatus)) return;

                var ts = (timestamp ?? DateTime.UtcNow).ToUniversalTime();

                if (_latestRealtimeStatusByUser.TryGetValue(key, out var existing))
                {
                    if (ts < existing.Timestamp)
                    {
                        return;
                    }
                }

                _latestRealtimeStatusByUser[key] = (normalizedStatus, ts);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PauseTabViewModel] ApplyFileStatusUpdate error: {ex.Message}");
            }
        }

        public void ApplySessionUpdate(string username, DateTime? loginAt, DateTime? logoutAt, double? totalDurationSeconds = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username)) return;
                var key = username.Trim();

                if (_latestSessionsByUser.TryGetValue(key, out var existing))
                {
                    DateTime? mergedLogin = existing.LoginAt;
                    if (loginAt.HasValue)
                    {
                        mergedLogin = !mergedLogin.HasValue
                            ? loginAt
                            : (loginAt.Value < mergedLogin.Value ? loginAt : mergedLogin);
                    }

                    // If any session is active, keep logoutAt null.
                    DateTime? mergedLogout = existing.LogoutAt;
                    if (!logoutAt.HasValue)
                    {
                        mergedLogout = null;
                    }
                    else if (mergedLogout.HasValue)
                    {
                        if (logoutAt.Value > mergedLogout.Value)
                        {
                            mergedLogout = logoutAt;
                        }
                    }
                    else
                    {
                        mergedLogout = logoutAt;
                    }

                    var mergedDurationMinutes = existing.TotalDurationMinutes;
                    if (totalDurationSeconds.HasValue && totalDurationSeconds.Value > 0)
                    {
                        var minutes = totalDurationSeconds.Value / 60.0;
                        mergedDurationMinutes = Math.Max(mergedDurationMinutes ?? 0, minutes);
                    }

                    _latestSessionsByUser[key] = (mergedLogin, mergedLogout, mergedDurationMinutes);
                }
                else
                {
                    double? totalDurationMinutes = null;
                    if (totalDurationSeconds.HasValue && totalDurationSeconds.Value > 0)
                    {
                        totalDurationMinutes = totalDurationSeconds.Value / 60.0;
                    }

                    _latestSessionsByUser[key] = (loginAt, logoutAt, totalDurationMinutes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PauseTabViewModel] ApplySessionUpdate error: {ex.Message}");
            }
        }

        public void RefreshData(List<LiveTrackingSessionModel> allSessions, bool includeInactiveFromSessionSnapshot = false)
        {
            try
            {
                if (allSessions == null) return;

                // Activity tab requirement: show only currently active users.
                var sessionUsernames = allSessions
                    .Select(s => (s.EmployeeName ?? string.Empty).Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var activeUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Active by session state: logoutAt == null
                foreach (var kv in _latestSessionsByUser)
                {
                    var n = (kv.Key ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    if (includeInactiveFromSessionSnapshot)
                    {
                        activeUsernames.Add(n);
                        continue;
                    }

                    if (kv.Value.LogoutAt == null && sessionUsernames.Contains(n))
                    {
                        activeUsernames.Add(n);
                    }
                }

                // Fallback: if session snapshot missing, infer active from running/paused file state
                foreach (var n in sessionUsernames)
                {
                    if (activeUsernames.Contains(n)) continue;
                    var userSessions = allSessions
                        .Where(s => string.Equals((s.EmployeeName ?? string.Empty).Trim(), n, StringComparison.OrdinalIgnoreCase));
                    var hasLiveFile = userSessions.Any(s =>
                        s.Files != null && s.Files.Any(f =>
                            IsWorkingStatus(f.FileStatus) ||
                            IsPausedStatus(f.FileStatus)));
                    if (hasLiveFile)
                    {
                        activeUsernames.Add(n);
                    }
                }

                // Build groups for active users only.
                var grouped = activeUsernames
                    .Select(username => new
                    {
                        Username = username,
                        Sessions = allSessions
                            .Where(s => string.Equals((s.EmployeeName ?? string.Empty).Trim(), username, StringComparison.OrdinalIgnoreCase))
                            .ToList(),
                    })
                    .OrderByDescending(g =>
                    {
                        var lastWorkLogUpdate = g.Sessions.Count > 0 ? g.Sessions.Max(x => x.UpdatedAt) : (DateTime?)null;
                        if (_latestSessionsByUser.TryGetValue(g.Username, out var ss))
                        {
                            var lastSession = ss.LogoutAt ?? DateTime.Now;
                            return new[] { lastWorkLogUpdate ?? DateTime.MinValue, lastSession }.Max();
                        }
                        return lastWorkLogUpdate ?? DateTime.MinValue;
                    })
                    .ToList();

                var newGroups = new List<PauseUserGroupModel>();
                var allReasons = new List<string>();
                double grandTotalPauseTime = 0;

                foreach (var group in grouped)
                {
                    var userSessions = group.Sessions ?? new List<LiveTrackingSessionModel>();

                    double totalWork = userSessions.Sum(s => s.TotalTimes);
                    double totalPause = userSessions.Sum(s => s.PauseTime);
                    int totalPauseCount = userSessions.Sum(s => s.PauseCount);
                    grandTotalPauseTime += totalPause;

                    DateTime? firstLogin = null;
                    DateTime? lastLogout = null;
                    double sessionDurationMinutes = 0;

                    if (_latestSessionsByUser.TryGetValue(group.Username, out var sessionInfo))
                    {
                        // Backend sends UTC; convert to local for consistent duration math
                        firstLogin = sessionInfo.LoginAt?.ToLocalTime();
                        lastLogout = sessionInfo.LogoutAt?.ToLocalTime();
                        sessionDurationMinutes = sessionInfo.TotalDurationMinutes ?? 0;
                    }
                    
                    var allFiles = userSessions.SelectMany(s => s.Files).ToList();
                    
                    if (!firstLogin.HasValue)
                    {
                        var starts = allFiles.Where(f => f.StartTime.HasValue).Select(f => f.StartTime!.Value).ToList();
                        if (starts.Any()) firstLogin = starts.Min();
                    }
                    
                    if (!lastLogout.HasValue)
                    {
                        var ends = allFiles.Where(f => f.EndTime.HasValue).Select(f => f.EndTime!.Value).ToList();
                        if (ends.Any()) lastLogout = ends.Max();
                    }

                    string status = "Login";
                    bool isWorking = false;
                    bool isPaused = false;

                    if (_latestRealtimeStatusByUser.TryGetValue(group.Username, out var liveStatus))
                    {
                        isPaused = IsPausedStatus(liveStatus.Status);
                        isWorking = !isPaused && IsWorkingStatus(liveStatus.Status);
                    }

                    if (!isPaused && !isWorking)
                    {
                        var latestLiveSession = userSessions
                            .OrderByDescending(s => s.UpdatedAt)
                            .FirstOrDefault(s =>
                                s.Files != null && s.Files.Any(f =>
                                    IsWorkingStatus(f.FileStatus) || IsPausedStatus(f.FileStatus)));

                        if (latestLiveSession?.Files != null)
                        {
                            isPaused = latestLiveSession.Files.Any(f => IsPausedStatus(f.FileStatus));
                            isWorking = !isPaused && latestLiveSession.Files.Any(f => IsWorkingStatus(f.FileStatus));
                        }
                        else
                        {
                            isPaused = allFiles.Any(f => IsPausedStatus(f.FileStatus));
                            isWorking = !isPaused && allFiles.Any(f => IsWorkingStatus(f.FileStatus));
                        }
                    }
                    
                    if (isWorking)
                    {
                        status = "Working";
                    }
                    else if (isPaused)
                    {
                        status = "Paused";
                    }
                    else if (lastLogout.HasValue)
                    {
                        status = "Logout";
                    }

                    var maxUpdated = userSessions.Count > 0 ? userSessions.Max(s => s.UpdatedAt).ToLocalTime() : DateTime.MinValue;
                    if ((!lastLogout.HasValue || maxUpdated > lastLogout.Value) && isWorking)
                    {
                        // If user is currently working, treat last activity as "still online".
                        lastLogout = maxUpdated;
                    }
                    
                    // Fallback to CreatedAt if no starts
                    if (!firstLogin.HasValue && userSessions.Any())
                    {
                        firstLogin = userSessions.Min(s => s.CreatedAt).ToLocalTime();
                    }

                    double totalDurationToday = 0;
                    if (firstLogin.HasValue)
                    {
                        var effectiveEnd = lastLogout ?? DateTime.Now;
                        if (effectiveEnd >= firstLogin.Value)
                        {
                            totalDurationToday = (effectiveEnd - firstLogin.Value).TotalMinutes;
                        }
                    }

                    if (sessionDurationMinutes > 0)
                    {
                        totalDurationToday = Math.Max(totalDurationToday, sessionDurationMinutes);
                    }

                    double idleTime = totalDurationToday - (totalWork + totalPause);
                    if (idleTime < 0) idleTime = 0; // If they work/pause more than session duration, idle is 0

                    var existingGroup = _pauseGroups.FirstOrDefault(g => g.EmployeeName == group.Username);
                    bool isExpanded = existingGroup != null && existingGroup.IsExpanded;

                    var userGroup = existingGroup ?? new PauseUserGroupModel();
                    
                    userGroup.EmployeeName = group.Username;
                    userGroup.TotalWorkTime = totalWork;
                    userGroup.TotalPauseTime = totalPause;
                    userGroup.PauseCount = totalPauseCount;
                    userGroup.FirstLogin = firstLogin;
                    userGroup.LastLogout = lastLogout;
                    userGroup.TotalDurationToday = totalDurationToday;
                    userGroup.IdleTime = idleTime;
                    userGroup.Status = status;
                    userGroup.IsExpanded = isExpanded;
                    
                    userGroup.Pauses.Clear();

                    foreach (var s in userSessions.Where(s => s.PauseCount > 0).OrderByDescending(s => s.UpdatedAt))
                    {
                        string reasonStr = s.LatestPauseReason;
                        if (string.IsNullOrWhiteSpace(reasonStr) || reasonStr == "—")
                        {
                            var validReasons = s.PauseReasons.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
                            reasonStr = validReasons.Any() ? string.Join(", ", validReasons.Distinct()) : "Unknown";
                        }
                        
                        // Collect reasons for summary cards
                        foreach(var pr in s.PauseReasons.Where(r => !string.IsNullOrWhiteSpace(r)))
                        {
                            allReasons.Add(pr);
                        }

                        userGroup.Pauses.Add(new PauseDetailModel 
                        { 
                            Reason = reasonStr,
                            ClientCode = string.IsNullOrWhiteSpace(s.ClientCode) ? "—" : s.ClientCode,
                            WorkType = string.IsNullOrWhiteSpace(s.WorkType) ? "—" : s.WorkType,
                            StartTime = s.CreatedAt,
                            EndTime = s.UpdatedAt,
                            Duration = s.PauseTime,
                            PauseCount = s.PauseCount
                        });
                    }

                    newGroups.Add(userGroup);
                }

                if (!includeInactiveFromSessionSnapshot)
                {
                    newGroups = newGroups
                        .Where(g => g != null
                            && (string.Equals(g.Status, "Paused", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(g.Status, "Working", StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }

                TotalPausedUsers = newGroups.Count(g => string.Equals(g.Status, "Paused", StringComparison.OrdinalIgnoreCase));
                TotalPauseTimeFormatted = LiveTrackingFileModel.FormatMinutes(grandTotalPauseTime);

                if (TotalPausedUsers > 0)
                {
                    AvgPauseTimeFormatted = LiveTrackingFileModel.FormatMinutes(grandTotalPauseTime / TotalPausedUsers);

                    if (allReasons.Any())
                    {
                        TopPauseReason = allReasons.GroupBy(r => r).OrderByDescending(g => g.Count()).First().Key;
                    }
                    else
                    {
                        TopPauseReason = "—";
                    }
                }
                else
                {
                    AvgPauseTimeFormatted = "0m";
                    TopPauseReason = "—";
                }

                // Sync UI collection in place and keep correct sort order (most recent at top)
                var newKeys = newGroups.Select(g => g.EmployeeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                for (int i = _pauseGroups.Count - 1; i >= 0; i--)
                {
                    if (!newKeys.Contains(_pauseGroups[i].EmployeeName))
                    {
                        _pauseGroups.RemoveAt(i);
                    }
                }

                for (int targetIndex = 0; targetIndex < newGroups.Count; targetIndex++)
                {
                    var incoming = newGroups[targetIndex];
                    var existing = _pauseGroups.FirstOrDefault(g => string.Equals(g.EmployeeName, incoming.EmployeeName, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        _pauseGroups.Insert(targetIndex, incoming);
                        continue;
                    }

                    var currentIndex = _pauseGroups.IndexOf(existing);
                    incoming.IsExpanded = existing.IsExpanded;
                    _pauseGroups[currentIndex] = incoming;
                    if (currentIndex != targetIndex)
                    {
                        _pauseGroups.Move(currentIndex, targetIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PauseTabViewModel] RefreshData error: {ex.Message}");
            }
        }

        private static bool IsWorkingStatus(string? status)
        {
            var s = (status ?? string.Empty).Trim();
            return string.Equals(s, "working", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "in_progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "in progress", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPausedStatus(string? status)
        {
            var s = (status ?? string.Empty).Trim();
            return string.Equals(s, "pause", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "paused", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "break", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "on_break", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "on break", StringComparison.OrdinalIgnoreCase);
        }
    }
}
