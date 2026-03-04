using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;
using SCHLStudio.App.ViewModels.LiveTracking.Services;

namespace SCHLStudio.App.ViewModels.LiveTracking.Tabs
{
    public sealed class UserSummaryTabViewModel : ViewModelBase
    {
        private readonly ObservableCollection<UserSummaryRowModel> _users = new();
        public ReadOnlyObservableCollection<UserSummaryRowModel> Users { get; }

        private readonly ObservableCollection<UserSummaryRowModel> _filteredUsers = new();
        public ReadOnlyObservableCollection<UserSummaryRowModel> FilteredUsers { get; }

        private readonly ObservableCollection<LiveTrackingSessionModel> _selectedWorkLogs = new();
        public ReadOnlyObservableCollection<LiveTrackingSessionModel> SelectedWorkLogs { get; }

        private readonly ObservableCollection<PauseDetailModel> _selectedUserPauses = new();
        public ReadOnlyObservableCollection<PauseDetailModel> SelectedUserPauses { get; }

        private readonly ObservableCollection<string> _selectedUserAllPauseReasons = new();
        public ReadOnlyObservableCollection<string> SelectedUserAllPauseReasons { get; }

        // ─── Search ───
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplySearchFilter();
            }
        }

        // ─── Date filter ───
        private DateTime? _filterDateFrom;
        public DateTime? FilterDateFrom
        {
            get => _filterDateFrom;
            set => SetProperty(ref _filterDateFrom, value);
        }

        private DateTime? _filterDateTo;
        public DateTime? FilterDateTo
        {
            get => _filterDateTo;
            set => SetProperty(ref _filterDateTo, value);
        }

        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }

        // ─── Selected user ───
        private UserSummaryRowModel? _selectedUser;
        public UserSummaryRowModel? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                    UpdateSelectedDetails();
            }
        }

        // ─── Last data for re-filtering ───
        private List<LiveTrackingSessionModel> _lastFilteredWorkLogs = new();
        private List<TrackerUserSessionModel> _lastSessions = new();
        private HashSet<string>? _savedExpandedPauseKeys;
        private bool _isRebuilding;

        public UserSummaryTabViewModel()
        {
            Users = new ReadOnlyObservableCollection<UserSummaryRowModel>(_users);
            FilteredUsers = new ReadOnlyObservableCollection<UserSummaryRowModel>(_filteredUsers);
            SelectedWorkLogs = new ReadOnlyObservableCollection<LiveTrackingSessionModel>(_selectedWorkLogs);
            SelectedUserPauses = new ReadOnlyObservableCollection<PauseDetailModel>(_selectedUserPauses);
            SelectedUserAllPauseReasons = new ReadOnlyObservableCollection<string>(_selectedUserAllPauseReasons);
            ApplyFilterCommand = new RelayCommand(_ => RebuildAll());
            ClearFilterCommand = new RelayCommand(_ => ClearFilter());
        }

        public void RefreshData(List<LiveTrackingSessionModel> filteredWorkLogs, List<TrackerUserSessionModel> sessions)
        {
            try
            {
                _lastFilteredWorkLogs = filteredWorkLogs ?? new List<LiveTrackingSessionModel>();
                _lastSessions = sessions ?? new List<TrackerUserSessionModel>();
                RebuildAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSummaryTabViewModel] RefreshData error: {ex.Message}");
            }
        }

        private void RebuildAll()
        {
            try
            {
                var prevSelectedKey = _selectedUser?.UserKey;

                var logs = ApplyDateFilter(_lastFilteredWorkLogs);

                var rows = BuildRows(logs, _lastSessions);

                // Save expanded pause keys BEFORE clearing users (which triggers SelectedUser=null → UpdateSelectedDetails → clears pauses)
                _savedExpandedPauseKeys = new HashSet<string>(
                    _selectedUserPauses
                        .Where(p => p.IsExpanded)
                        .Select(p => $"{p.ClientCode}|{p.WorkType}|{p.StartTime:yyyy-MM-dd HH:mm:ss}"),
                    StringComparer.Ordinal);

                // Guard: prevent intermediate cascades from _users.Clear() / ApplySearchFilter()
                // from consuming _savedExpandedPauseKeys prematurely.
                _isRebuilding = true;

                _users.Clear();
                foreach (var r in rows) _users.Add(r);

                ApplySearchFilter();

                // Lift the guard before the final selection restore so that
                // UpdateSelectedDetails runs exactly once with the saved keys.
                _isRebuilding = false;

                // Restore selection
                if (!string.IsNullOrWhiteSpace(prevSelectedKey))
                {
                    var match = _filteredUsers.FirstOrDefault(u =>
                        string.Equals(u.UserKey, prevSelectedKey, StringComparison.OrdinalIgnoreCase));
                    if (match != null) SelectedUser = match;
                    else if (_filteredUsers.Count > 0) SelectedUser = _filteredUsers[0];
                    else SelectedUser = null;
                }
                else if (_filteredUsers.Count > 0)
                {
                    SelectedUser = _filteredUsers[0];
                }
            }
            catch (Exception ex)
            {
                _isRebuilding = false;
                Debug.WriteLine($"[UserSummaryTabViewModel] RebuildAll error: {ex.Message}");
            }
        }

        private void ApplySearchFilter()
        {
            try
            {
                var query = (_searchText ?? string.Empty).Trim();
                _filteredUsers.Clear();
                foreach (var u in _users)
                {
                    if (string.IsNullOrWhiteSpace(query)
                        || (u.EmployeeName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _filteredUsers.Add(u);
                    }
                }

                // Re-select
                if (SelectedUser != null && !_filteredUsers.Contains(SelectedUser) && _filteredUsers.Count > 0)
                    SelectedUser = _filteredUsers[0];
                else if (_filteredUsers.Count > 0 && SelectedUser == null)
                    SelectedUser = _filteredUsers[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSummaryTabViewModel] ApplySearchFilter error: {ex.Message}");
            }
        }

        private void ClearFilter()
        {
            FilterDateFrom = null;
            FilterDateTo = null;
            SearchText = string.Empty;
            RebuildAll();
        }

        private void UpdateSelectedDetails()
        {
            // Skip intermediate calls triggered by _users.Clear() / ApplySearchFilter() cascade.
            // The real call happens after _isRebuilding is set back to false.
            if (_isRebuilding) return;

            try
            {
                // Consume saved expanded keys (saved in RebuildAll before the clear-cascade)
                var expandedKeys = _savedExpandedPauseKeys ?? new HashSet<string>(StringComparer.Ordinal);
                _savedExpandedPauseKeys = null;

                _selectedWorkLogs.Clear();
                _selectedUserPauses.Clear();
                _selectedUserAllPauseReasons.Clear();
                if (_selectedUser == null) return;

                var userKey = _selectedUser.UserKey;
                var logs = ApplyDateFilter(_lastFilteredWorkLogs);

                var userLogs = logs
                    .Where(l => string.Equals(NormalizeUserKey(l.EmployeeName), userKey, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(l => l.UpdatedAt)
                    .ToList();

                foreach (var wl in userLogs)
                    _selectedWorkLogs.Add(wl);

                var seenReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var reason in userLogs
                    .SelectMany(l => l.PauseReasons ?? new List<string>())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim()))
                {
                    if (seenReasons.Add(reason))
                    {
                        _selectedUserAllPauseReasons.Add(reason);
                    }
                }

                // Build pause details per work log session (expandable rows)
                foreach (var wl in userLogs.Where(l => l.PauseCount > 0 || l.PauseTime > 0))
                {
                    var pauseReasons = (wl.PauseReasons ?? new List<string>())
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .Select(r => r.Trim())
                        .ToList();

                    var detail = new PauseDetailModel
                    {
                        Reason = "All",
                        PauseReasons = pauseReasons,
                        ClientCode = wl.ClientCode ?? "—",
                        WorkType = wl.WorkTypeDisplay ?? "—",
                        StartTime = wl.CreatedAt,
                        EndTime = wl.UpdatedAt != default ? (DateTime?)wl.UpdatedAt : null,
                        Duration = wl.PauseTime,
                        PauseCount = wl.PauseCount,
                    };

                    // Restore expanded state
                    var key = $"{detail.ClientCode}|{detail.WorkType}|{detail.StartTime:yyyy-MM-dd HH:mm:ss}";
                    if (expandedKeys.Contains(key))
                        detail.IsExpanded = true;

                    // Populate expandable sub-items with full data from parent session
                    if (wl.PauseReasonDetails != null)
                    {
                        foreach (var item in wl.PauseReasonDetails)
                        {
                            item.ClientCode = wl.ClientCode ?? "—";
                            item.WorkType = wl.WorkTypeDisplay ?? "—";
                            item.PauseCount = wl.PauseCount;
                            detail.Items.Add(item);
                        }
                    }

                    _selectedUserPauses.Add(detail);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSummaryTabViewModel] UpdateSelectedDetails error: {ex.Message}");
            }
        }

        private List<LiveTrackingSessionModel> ApplyDateFilter(List<LiveTrackingSessionModel> logs)
        {
            // Default behavior for User Summary: TODAY ONLY.
            // If the user explicitly selects a date range, use that instead.

            DateTime from;
            DateTime to;
            if (!_filterDateFrom.HasValue && !_filterDateTo.HasValue)
            {
                from = DateTime.Today;
                to = DateTime.Today.AddDays(1);
            }
            else
            {
                // Single date: if only one is set, use same for both
                from = (_filterDateFrom ?? _filterDateTo)?.Date ?? DateTime.Today;
                to = ((_filterDateTo ?? _filterDateFrom)?.Date ?? DateTime.Today).AddDays(1);
            }

            return logs.Where(l =>
            {
                var dt = LiveTrackingFileModel.ToDhakaTime(l.UpdatedAt).Date;
                return dt >= from && dt < to;
            }).ToList();
        }

        // ──────────────────────────────────────────────────────────────

        private static string NormalizeUserKey(string? username)
        {
            try
            {
                var raw = (username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
                var dash = raw.IndexOf('-');
                if (dash > 0)
                {
                    var left = raw.Substring(0, dash).Trim();
                    if (!string.IsNullOrWhiteSpace(left)) raw = left;
                }
                return raw.Trim().ToLowerInvariant();
            }
            catch { return (username ?? string.Empty).Trim().ToLowerInvariant(); }
        }

        private static List<UserSummaryRowModel> BuildRows(
            List<LiveTrackingSessionModel> filteredWorkLogs,
            List<TrackerUserSessionModel> sessions)
        {
            static double ClampDurationMinutesToToday(DateTime? firstLoginAt, DateTime? lastLogoutAt, bool isActive)
            {
                try
                {
                    if (!firstLoginAt.HasValue) return 0;

                    var todayStart = DateTime.Today;
                    var todayEnd = todayStart.AddDays(1);

                    var startLocal = LiveTrackingFileModel.ToDhakaTime(firstLoginAt.Value);
                    var endLocal = isActive
                        ? DateTime.Now
                        : (lastLogoutAt.HasValue ? LiveTrackingFileModel.ToDhakaTime(lastLogoutAt.Value) : DateTime.Now);

                    if (endLocal > todayEnd) endLocal = todayEnd;
                    if (startLocal < todayStart) startLocal = todayStart;
                    if (endLocal < startLocal) return 0;

                    var mins = (endLocal - startLocal).TotalMinutes;
                    return mins > 0 ? mins : 0;
                }
                catch
                {
                    return 0;
                }
            }

            var displayNameByUser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in sessions)
            {
                if (s == null || string.IsNullOrWhiteSpace(s.Username)) continue;
                var key = NormalizeUserKey(s.Username);
                if (!string.IsNullOrWhiteSpace(key) && !displayNameByUser.ContainsKey(key))
                    displayNameByUser[key] = s.Username.Trim();
            }
            foreach (var wl in filteredWorkLogs)
            {
                if (wl == null || string.IsNullOrWhiteSpace(wl.EmployeeName)) continue;
                var key = NormalizeUserKey(wl.EmployeeName);
                if (!string.IsNullOrWhiteSpace(key) && !displayNameByUser.ContainsKey(key))
                    displayNameByUser[key] = wl.EmployeeName.Trim();
            }

            var workLogsByUser = filteredWorkLogs
                .Where(w => w != null && !string.IsNullOrWhiteSpace(w.EmployeeName))
                .GroupBy(w => NormalizeUserKey(w.EmployeeName))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var sessionByUser = sessions
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Username))
                .GroupBy(s => NormalizeUserKey(s.Username))
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        FirstLoginAt = g.Where(x => x.FirstLoginAt.HasValue).Select(x => x.FirstLoginAt!.Value).OrderBy(x => x).FirstOrDefault(),
                        LastLogoutAt = g.Where(x => x.LastLogoutAt.HasValue).Select(x => x.LastLogoutAt!.Value).OrderByDescending(x => x).FirstOrDefault(),
                        IsActive = g.Any(x => x.IsActive),
                        TotalDurationSeconds = g.Where(x => x.TotalDurationSeconds > 0).Sum(x => x.TotalDurationSeconds),
                    },
                    StringComparer.OrdinalIgnoreCase
                );

            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in workLogsByUser.Keys) allKeys.Add(k);
            foreach (var k in sessionByUser.Keys) allKeys.Add(k);

            var rows = new List<UserSummaryRowModel>();
            foreach (var key in allKeys)
            {
                var logs = workLogsByUser.TryGetValue(key, out var list) ? list : new List<LiveTrackingSessionModel>();
                var totalWork = logs.Sum(l => l.ComputedTotalTimes);
                var totalPause = logs.Sum(l => l.PauseTime);
                var completedFiles = logs
                    .SelectMany(l => l.Files)
                    .Count(f => f != null && string.Equals(f.FileStatus, "done", StringComparison.OrdinalIgnoreCase));

                double idleMinutes = 0;
                DateTime? firstLogin = null;
                DateTime? lastLogout = null;
                var statusText = "—";
                double totalDurationMinutes = 0;

                if (sessionByUser.TryGetValue(key, out var sess))
                {
                    firstLogin = sess.FirstLoginAt == default ? null : sess.FirstLoginAt;
                    lastLogout = sess.LastLogoutAt == default ? null : sess.LastLogoutAt;
                    statusText = sess.IsActive ? "Active" : "Logout";

                    // Dashboard/session APIs may contain timestamps spanning multiple days.
                    // User Summary must show TODAY ONLY, so clamp duration to today's window.
                    var durationByTimes = ClampDurationMinutesToToday(firstLogin, lastLogout, sess.IsActive);

                    // Fallback to backend-provided seconds if we can't compute from timestamps.
                    if (durationByTimes > 0)
                    {
                        totalDurationMinutes = durationByTimes;
                    }
                    else if (sess.TotalDurationSeconds > 0)
                    {
                        totalDurationMinutes = sess.TotalDurationSeconds / 60.0;
                    }
                    else
                    {
                        totalDurationMinutes = 0;
                    }

                    idleMinutes = Math.Max(0, totalDurationMinutes - totalWork - totalPause);
                }

                rows.Add(new UserSummaryRowModel
                {
                    UserKey = key,
                    EmployeeName = displayNameByUser.TryGetValue(key, out var display) ? display : key,
                    TotalWorkMinutes = totalWork,
                    TotalPauseMinutes = totalPause,
                    IdleMinutes = idleMinutes,
                    TotalFiles = completedFiles,
                    FirstLoginAt = firstLogin,
                    LastLogoutAt = lastLogout,
                    TotalDurationTodayMinutes = totalDurationMinutes,
                    StatusText = statusText,
                    PauseCount = logs.Sum(l => l.PauseCount),
                });
            }

            return rows
                .OrderByDescending(r => r.TotalWorkMinutes)
                .ThenBy(r => r.EmployeeName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public sealed class UserSummaryRowModel : ViewModelBase
    {
        private string _userKey = string.Empty;
        public string UserKey { get => _userKey; set => SetProperty(ref _userKey, value); }

        private string _employeeName = string.Empty;
        public string EmployeeName
        {
            get => _employeeName;
            set { if (SetProperty(ref _employeeName, value)) OnPropertyChanged(nameof(EmployeeNameDisplay)); }
        }

        private double _totalWorkMinutes;
        public double TotalWorkMinutes
        {
            get => _totalWorkMinutes;
            set { if (SetProperty(ref _totalWorkMinutes, value)) OnPropertyChanged(nameof(TotalWorkFormatted)); }
        }

        private double _totalPauseMinutes;
        public double TotalPauseMinutes
        {
            get => _totalPauseMinutes;
            set { if (SetProperty(ref _totalPauseMinutes, value)) OnPropertyChanged(nameof(TotalPauseFormatted)); }
        }

        private double _idleMinutes;
        public double IdleMinutes
        {
            get => _idleMinutes;
            set { if (SetProperty(ref _idleMinutes, value)) OnPropertyChanged(nameof(IdleFormatted)); }
        }

        private int _totalFiles;
        public int TotalFiles { get => _totalFiles; set => SetProperty(ref _totalFiles, value); }

        private int _pauseCount;
        public int PauseCount { get => _pauseCount; set => SetProperty(ref _pauseCount, value); }

        private DateTime? _firstLoginAt;
        public DateTime? FirstLoginAt
        {
            get => _firstLoginAt;
            set { if (SetProperty(ref _firstLoginAt, value)) OnPropertyChanged(nameof(FirstLoginFormatted)); }
        }

        private DateTime? _lastLogoutAt;
        public DateTime? LastLogoutAt
        {
            get => _lastLogoutAt;
            set { if (SetProperty(ref _lastLogoutAt, value)) OnPropertyChanged(nameof(LastLogoutFormatted)); }
        }

        private double _totalDurationTodayMinutes;
        public double TotalDurationTodayMinutes
        {
            get => _totalDurationTodayMinutes;
            set { if (SetProperty(ref _totalDurationTodayMinutes, value)) OnPropertyChanged(nameof(TotalDurationTodayFormatted)); }
        }

        private string _statusText = "—";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        // Formatted
        public string EmployeeNameDisplay => string.IsNullOrEmpty(EmployeeName) ? "—" : EmployeeName;
        public string TotalWorkFormatted => LiveTrackingFileModel.FormatMinutes(TotalWorkMinutes);
        public string TotalPauseFormatted => LiveTrackingFileModel.FormatMinutes(TotalPauseMinutes);
        public string IdleFormatted => LiveTrackingFileModel.FormatMinutes(IdleMinutes);
        public string FirstLoginFormatted => FirstLoginAt.HasValue ? LiveTrackingFileModel.ToDhakaTime(FirstLoginAt.Value).ToString("hh:mm tt") : "—";
        public string LastLogoutFormatted => LastLogoutAt.HasValue ? LiveTrackingFileModel.ToDhakaTime(LastLogoutAt.Value).ToString("hh:mm tt") : "—";
        public string TotalDurationTodayFormatted => LiveTrackingFileModel.FormatMinutes(TotalDurationTodayMinutes);
    }
}
