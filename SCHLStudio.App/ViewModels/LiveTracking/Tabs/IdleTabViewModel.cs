using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;
using SCHLStudio.App.ViewModels.LiveTracking.Services;

namespace SCHLStudio.App.ViewModels.LiveTracking.Tabs
{
    public sealed class IdleTabViewModel : ViewModelBase
    {
        private readonly ObservableCollection<IdleUserModel> _idleUsers = new();
        public ReadOnlyObservableCollection<IdleUserModel> IdleUsers { get; }

        private static string NormalizeUserKey(string? username)
        {
            try
            {
                var raw = (username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

                // Common format: "e123 - Real Name". Worklogs often store only "e123".
                var dash = raw.IndexOf('-');
                if (dash > 0)
                {
                    var left = raw.Substring(0, dash).Trim();
                    if (!string.IsNullOrWhiteSpace(left))
                    {
                        raw = left;
                    }
                }

                return raw.Trim().ToLowerInvariant();
            }
            catch
            {
                return (username ?? string.Empty).Trim().ToLowerInvariant();
            }
        }

        private int _totalIdleUsers;
        public int TotalIdleUsers
        {
            get => _totalIdleUsers;
            private set => SetProperty(ref _totalIdleUsers, value);
        }

        private string _totalIdleDuration = "0m";
        public string TotalIdleDuration
        {
            get => _totalIdleDuration;
            private set => SetProperty(ref _totalIdleDuration, value);
        }

        public IdleTabViewModel()
        {
            IdleUsers = new ReadOnlyObservableCollection<IdleUserModel>(_idleUsers);
        }

        public void RefreshData(
            List<LiveTrackingSessionModel> filteredWorkLogs,
            List<TrackerUserSessionModel> sessions,
            string selectedShift,
            string autoDetectedShift)
        {
            var rows = new List<IdleUserModel>();
            var selected = (selectedShift ?? string.Empty).Trim();
            var autoShift = (autoDetectedShift ?? string.Empty).Trim();

            var displayNameByUser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var s in (sessions ?? new List<TrackerUserSessionModel>()))
                {
                    if (s == null || string.IsNullOrWhiteSpace(s.Username)) continue;
                    var key = NormalizeUserKey(s.Username);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!displayNameByUser.ContainsKey(key))
                    {
                        displayNameByUser[key] = s.Username.Trim();
                    }
                }

                foreach (var wl in (filteredWorkLogs ?? new List<LiveTrackingSessionModel>()))
                {
                    if (wl == null || string.IsNullOrWhiteSpace(wl.EmployeeName)) continue;
                    var key = NormalizeUserKey(wl.EmployeeName);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!displayNameByUser.ContainsKey(key))
                    {
                        displayNameByUser[key] = wl.EmployeeName.Trim();
                    }
                }
            }
            catch
            {
            }

            var latestStatusByUser = (filteredWorkLogs ?? new List<LiveTrackingSessionModel>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.EmployeeName))
                .GroupBy(s => NormalizeUserKey(s.EmployeeName))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .SelectMany(s => s.Files ?? new ObservableCollection<LiveTrackingFileModel>())
                        .OrderByDescending(f => f.EndTime ?? f.StartTime ?? DateTime.MinValue)
                        .Select(f => (f.FileStatus ?? string.Empty).Trim())
                        .FirstOrDefault() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase
                );

            var hasLiveStatusByUser = (filteredWorkLogs ?? new List<LiveTrackingSessionModel>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.EmployeeName))
                .GroupBy(s => NormalizeUserKey(s.EmployeeName))
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .SelectMany(s => s.Files ?? new ObservableCollection<LiveTrackingFileModel>())
                        .Any(f =>
                        {
                            var st = (f?.FileStatus ?? string.Empty).Trim();
                            return IsWorkingStatus(st) || IsPausedStatus(st);
                        }),
                    StringComparer.OrdinalIgnoreCase
                );

            var lastWorkAtByUser = (filteredWorkLogs ?? new List<LiveTrackingSessionModel>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.EmployeeName))
                .GroupBy(s => NormalizeUserKey(s.EmployeeName))
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var candidates = new List<DateTime>();

                        foreach (var session in g)
                        {
                            if (session.UpdatedAt != default)
                            {
                                candidates.Add(ToUtcSafe(session.UpdatedAt));
                            }

                            if (session.Files != null)
                            {
                                foreach (var file in session.Files)
                                {
                                    if (file.EndTime.HasValue)
                                    {
                                        candidates.Add(ToUtcSafe(file.EndTime.Value));
                                    }
                                    else if (file.StartTime.HasValue)
                                    {
                                        candidates.Add(ToUtcSafe(file.StartTime.Value));
                                    }
                                }
                            }
                        }

                        if (candidates.Count == 0) return (DateTime?)null;
                        return candidates.Max();
                    },
                    StringComparer.OrdinalIgnoreCase
                );

            var usersWithShiftData = latestStatusByUser
                .Keys
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var activeSessions = (sessions ?? new List<TrackerUserSessionModel>())
                .Where(s => s != null && s.IsActive && !string.IsNullOrWhiteSpace(s.Username))
                .GroupBy(s => NormalizeUserKey(s.Username))
                .Select(g => new
                {
                    Username = g.Key,
                    FirstLogin = g.Where(x => x.FirstLoginAt.HasValue).Select(x => x.FirstLoginAt!.Value).OrderBy(x => x).FirstOrDefault(),
                    TotalDurationSeconds = g.Where(x => x.TotalDurationSeconds > 0).Sum(x => x.TotalDurationSeconds),
                })
                .ToList();

            foreach (var s in activeSessions)
            {
                if (!string.Equals(selected, "All Shifts", StringComparison.OrdinalIgnoreCase))
                {
                    var hasShiftWorklog = usersWithShiftData.Contains(s.Username);
                    var assumeCurrentShiftIdle =
                        !hasShiftWorklog
                        && string.Equals(selected, autoShift, StringComparison.OrdinalIgnoreCase);

                    if (!hasShiftWorklog && !assumeCurrentShiftIdle)
                    {
                        continue;
                    }
                }

                var latestStatus = latestStatusByUser.TryGetValue(s.Username, out var status)
                    ? (status ?? string.Empty).Trim()
                    : string.Empty;

                if (hasLiveStatusByUser.TryGetValue(s.Username, out var hasLive) && hasLive)
                {
                    continue;
                }

                var durationMinutes = 0d;
                if (lastWorkAtByUser.TryGetValue(s.Username, out var lastWorkAt) && lastWorkAt.HasValue)
                {
                    var mins = (DateTime.UtcNow - lastWorkAt.Value).TotalMinutes;
                    durationMinutes = mins > 0 ? mins : 0;
                }
                else if (s.FirstLogin != default)
                {
                    var mins = (DateTime.UtcNow - s.FirstLogin).TotalMinutes;
                    durationMinutes = mins > 0 ? mins : 0;
                }

                rows.Add(new IdleUserModel
                {
                    Username = displayNameByUser.TryGetValue(s.Username, out var display)
                        ? display
                        : s.Username,
                    FirstLogin = s.FirstLogin == default ? null : s.FirstLogin,
                    TotalDurationMinutes = durationMinutes,
                });
            }

            rows = rows
                .OrderByDescending(r => r.TotalDurationMinutes)
                .ThenBy(r => r.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _idleUsers.Clear();
            foreach (var row in rows)
            {
                _idleUsers.Add(row);
            }

            TotalIdleUsers = rows.Count;
            TotalIdleDuration = LiveTrackingFileModel.FormatMinutes(rows.Sum(x => x.TotalDurationMinutes));
        }

        private static bool IsWorkingStatus(string? status)
        {
            var s = (status ?? string.Empty).Trim();
            return string.Equals(s, "working", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "in_progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "in progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "in-progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "inprogress", StringComparison.OrdinalIgnoreCase);
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

        private static DateTime ToUtcSafe(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc) return dateTime;
            if (dateTime.Kind == DateTimeKind.Local) return dateTime.ToUniversalTime();
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime();
        }
    }
}
