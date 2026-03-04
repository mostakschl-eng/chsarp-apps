using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;
using SCHLStudio.App.ViewModels.LiveTracking.Services;
using SCHLStudio.App.ViewModels.LiveTracking.Tabs;

namespace SCHLStudio.App.ViewModels.LiveTracking
{
    public sealed class LiveTrackingViewModel : ViewModelBase
    {
        public static LiveTrackingViewModel Shared { get; } = new LiveTrackingViewModel();

        private enum RangeTarget
        {
            From,
            To,
        }

        private readonly ILiveTrackingDataService _dataService;
        private readonly DispatcherTimer _uiRefreshTimer;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private bool _isTrackingStarted;

        public ClientTabViewModel ClientTab { get; }
        public ProductionTabViewModel ProductionTab { get; }
        public QcTabViewModel QcTab { get; }
        public PauseTabViewModel PauseTab { get; }
        public IdleTabViewModel IdleTab { get; }
        public ProductivityTabViewModel ProductivityTab { get; }
        public UserSummaryTabViewModel UserSummaryTab { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // --- Global Filters (in header, applied to all tabs) ---

        private bool _isSingleDatePopupOpen;
        public bool IsSingleDatePopupOpen
        {
            get => _isSingleDatePopupOpen;
            set => SetProperty(ref _isSingleDatePopupOpen, value);
        }

        private DateTime? _pendingSingleDate;
        public DateTime? PendingSingleDate
        {
            get => _pendingSingleDate;
            set => SetProperty(ref _pendingSingleDate, value);
        }

        public string SingleDateDisplay => SelectedDate.ToString("dd/MM/yyyy");

        public RelayCommand ToggleSingleDatePopupCommand { get; }
        public RelayCommand ApplySingleDateCommand { get; }
        public RelayCommand ClearSingleDateCommand { get; }
        public RelayCommand TodaySingleDateCommand { get; }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetProperty(ref _dateFrom, value))
                {
                    OnPropertyChanged(nameof(DateRangeDisplay));

                    if (value.HasValue && !DateTo.HasValue)
                    {
                        DateTo = value;
                    }
                }
            }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set
            {
                if (SetProperty(ref _dateTo, value))
                {
                    OnPropertyChanged(nameof(DateRangeDisplay));

                    if (value.HasValue && !DateFrom.HasValue)
                    {
                        DateFrom = value;
                    }
                }
            }
        }

        private bool _isDateRangePopupOpen;
        public bool IsDateRangePopupOpen
        {
            get => _isDateRangePopupOpen;
            set => SetProperty(ref _isDateRangePopupOpen, value);
        }

        private RangeTarget _activeRangeTarget = RangeTarget.From;
        private DateTime _rangePickerDate = DateTime.Today;
        public DateTime RangePickerDate
        {
            get => _rangePickerDate;
            set
            {
                if (SetProperty(ref _rangePickerDate, value))
                {
                    if (_activeRangeTarget == RangeTarget.From)
                    {
                        DateFrom = value.Date;
                    }
                    else
                    {
                        DateTo = value.Date;
                    }
                }
            }
        }

        public string DateFromDisplay => DateFrom.HasValue ? DateFrom.Value.ToString("dd/MM/yyyy") : "From";
        public string DateToDisplay => DateTo.HasValue ? DateTo.Value.ToString("dd/MM/yyyy") : "To";
        public bool IsRangeFromActive => _activeRangeTarget == RangeTarget.From;
        public bool IsRangeToActive => _activeRangeTarget == RangeTarget.To;

        public string DateRangeDisplay
        {
            get
            {
                if (DateFrom.HasValue && DateTo.HasValue)
                    return $"{DateFrom.Value:dd/MM/yyyy} - {DateTo.Value:dd/MM/yyyy}";

                return SelectedDate.ToString("dd/MM/yyyy");
            }
        }

        public RelayCommand ToggleDateRangePopupCommand { get; }
        public RelayCommand SetRangeTargetFromCommand { get; }
        public RelayCommand SetRangeTargetToCommand { get; }
        public RelayCommand ApplyDateRangeCommand { get; }
        public RelayCommand ClearDateRangeCommand { get; }
        public RelayCommand TodayDateRangeCommand { get; }
        public RelayCommand UseSingleDateCommand { get; }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    OnPropertyChanged(nameof(DateRangeDisplay));
                    OnPropertyChanged(nameof(SingleDateDisplay));
                    _ = LoadDataAsync();
                }
            }
        }

        // Client filter dropdown
        private readonly ObservableCollection<string> _availableClients = new();
        public ReadOnlyObservableCollection<string> AvailableClients { get; }

        private string _selectedClient = "All Clients";
        public string SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (SetProperty(ref _selectedClient, value))
                    ApplyGlobalFilters();
            }
        }

        // User filter dropdown
        private readonly ObservableCollection<string> _availableUsers = new();
        public ReadOnlyObservableCollection<string> AvailableUsers { get; }

        private string _selectedUser = "All Users";
        public string SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                    ApplyGlobalFilters();
            }
        }

        // Shift filter dropdown
        private readonly ObservableCollection<string> _availableShifts = new();
        public ReadOnlyObservableCollection<string> AvailableShifts { get; }

        private string _selectedShift = "All Shifts";
        public string SelectedShift
        {
            get => _selectedShift;
            set
            {
                if (SetProperty(ref _selectedShift, value))
                    ApplyGlobalFilters();
            }
        }

        private List<LiveTrackingSessionModel>? _allData;
        private List<TrackerUserSessionModel> _allSessionData = new();

        public LiveTrackingViewModel()
        {
            var apiClient = ((App)System.Windows.Application.Current).ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
            _dataService = new LiveTrackingDataService(apiClient);
            ClientTab = new ClientTabViewModel();
            ProductionTab = new ProductionTabViewModel();
            QcTab = new QcTabViewModel();
            PauseTab = new PauseTabViewModel();
            IdleTab = new IdleTabViewModel();
            ProductivityTab = new ProductivityTabViewModel();
            UserSummaryTab = new UserSummaryTabViewModel();

            AvailableClients = new ReadOnlyObservableCollection<string>(_availableClients);
            AvailableUsers = new ReadOnlyObservableCollection<string>(_availableUsers);
            AvailableShifts = new ReadOnlyObservableCollection<string>(_availableShifts);

            // Default to "All Shifts" so active sessions never disappear due to auto-detected shift
            // boundaries (e.g., 3PM switching morning -> evening).
            _selectedShift = "All Shifts";

            // UI refresh timer: re-applies filters to keep derived UI up to date.
            _uiRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3),
            };
            _uiRefreshTimer.Tick += (s, e) =>
            {
                try
                {
                    try
                    {
                        if (_allData != null)
                        {
                            foreach (var session in _allData)
                            {
                                try
                                {
                                    session?.NotifyLiveTick();
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    ApplyGlobalFilters();
                }
                catch
                {
                }
            };


            ToggleSingleDatePopupCommand = new RelayCommand(_ =>
            {
                PendingSingleDate = SelectedDate;
                IsSingleDatePopupOpen = !IsSingleDatePopupOpen;
                if (IsSingleDatePopupOpen) IsDateRangePopupOpen = false;
            });

            ApplySingleDateCommand = new RelayCommand(_ =>
            {
                var date = PendingSingleDate ?? SelectedDate;
                DateFrom = null;
                DateTo = null;
                SelectedDate = date.Date;
                IsSingleDatePopupOpen = false;
                OnPropertyChanged(nameof(DateRangeDisplay));
                OnPropertyChanged(nameof(SingleDateDisplay));
                _ = LoadDataAsync();
            });

            ClearSingleDateCommand = new RelayCommand(_ =>
            {
                PendingSingleDate = SelectedDate;
                IsSingleDatePopupOpen = false;
            });

            TodaySingleDateCommand = new RelayCommand(_ =>
            {
                DateFrom = null;
                DateTo = null;
                SelectedDate = DateTime.Today;
                PendingSingleDate = SelectedDate;
                IsSingleDatePopupOpen = false;
                OnPropertyChanged(nameof(DateRangeDisplay));
                OnPropertyChanged(nameof(SingleDateDisplay));
                _ = LoadDataAsync();
            });

            ToggleDateRangePopupCommand = new RelayCommand(_ =>
            {
                IsDateRangePopupOpen = !IsDateRangePopupOpen;
                if (IsDateRangePopupOpen) IsSingleDatePopupOpen = false;
            });

            SetRangeTargetFromCommand = new RelayCommand(_ =>
            {
                _activeRangeTarget = RangeTarget.From;
                OnPropertyChanged(nameof(IsRangeFromActive));
                OnPropertyChanged(nameof(IsRangeToActive));
                RangePickerDate = (DateFrom ?? DateTime.Today).Date;
            });

            SetRangeTargetToCommand = new RelayCommand(_ =>
            {
                _activeRangeTarget = RangeTarget.To;
                OnPropertyChanged(nameof(IsRangeFromActive));
                OnPropertyChanged(nameof(IsRangeToActive));
                RangePickerDate = (DateTo ?? (DateFrom ?? DateTime.Today)).Date;
            });

            ApplyDateRangeCommand = new RelayCommand(_ =>
            {
                // Range mode: ensure both ends exist; if only one is selected, treat as single-day range.
                if (DateFrom.HasValue && !DateTo.HasValue) DateTo = DateFrom;
                if (DateTo.HasValue && !DateFrom.HasValue) DateFrom = DateTo;
                if (!DateFrom.HasValue || !DateTo.HasValue) return;

                if (DateFrom.Value.Date > DateTo.Value.Date)
                {
                    var tmp = DateFrom;
                    DateFrom = DateTo;
                    DateTo = tmp;
                }

                IsDateRangePopupOpen = false;
                OnPropertyChanged(nameof(DateRangeDisplay));
                OnPropertyChanged(nameof(DateFromDisplay));
                OnPropertyChanged(nameof(DateToDisplay));
                _ = LoadDataAsync();
            });

            ClearDateRangeCommand = new RelayCommand(_ =>
            {
                DateFrom = null;
                DateTo = null;
                IsDateRangePopupOpen = false;
                OnPropertyChanged(nameof(DateRangeDisplay));
                OnPropertyChanged(nameof(DateFromDisplay));
                OnPropertyChanged(nameof(DateToDisplay));
                _ = LoadDataAsync();
            });

            TodayDateRangeCommand = new RelayCommand(_ =>
            {
                DateFrom = null;
                DateTo = null;
                SelectedDate = DateTime.Today;
                IsDateRangePopupOpen = false;
                OnPropertyChanged(nameof(DateRangeDisplay));
                OnPropertyChanged(nameof(DateFromDisplay));
                OnPropertyChanged(nameof(DateToDisplay));
                _ = LoadDataAsync();
            });

            UseSingleDateCommand = new RelayCommand(_ =>
            {
                var date = DateFrom ?? DateTo ?? SelectedDate;
                DateFrom = null;
                DateTo = null;
                SelectedDate = date.Date;
                IsDateRangePopupOpen = false;
                OnPropertyChanged(nameof(DateRangeDisplay));
                OnPropertyChanged(nameof(DateFromDisplay));
                OnPropertyChanged(nameof(DateToDisplay));
                _ = LoadDataAsync();
            });

            PendingSingleDate = SelectedDate;
            RangePickerDate = DateTime.Today;
        }

        public void StartTracking()
        {
            // Start socket + timer only once to avoid duplicate connections
            if (!_isTrackingStarted)
            {
                _isTrackingStarted = true;
                _ = _dataService.StartRealTimeUpdatesAsync(OnTrackerUpdated, OnReportUpdated, OnSessionUpdated);
            }

            if (!_uiRefreshTimer.IsEnabled)
            {
                _uiRefreshTimer.Start();
            }


            // Always do an immediate data load when this tab becomes active
            _ = LoadDataAsync();
        }

        public void StopTracking()
        {
            _isTrackingStarted = false;
            _uiRefreshTimer.Stop();
            _dataService.StopRealTimeUpdates();
        }

        public void SetForegroundMode()
        {
            if (!_uiRefreshTimer.IsEnabled)
                _uiRefreshTimer.Start();

            _ = LoadDataAsync();
        }


        // ── Shared JSON helpers ────────────────────────────────────────

        private static LiveTrackingFileModel ParseFileFromJson(JsonElement el)
        {
            var fname = (el.TryGetProperty("fileName", out var fnEl) || el.TryGetProperty("file_name", out fnEl))
                ? fnEl.GetString() ?? "" : "";
            string fStatus = string.Empty;
            if ((el.TryGetProperty("fileStatus", out var fStatusEl) || el.TryGetProperty("file_status", out fStatusEl))
                && fStatusEl.ValueKind == JsonValueKind.String)
            {
                fStatus = fStatusEl.GetString() ?? string.Empty;
            }

            var fTime = (el.TryGetProperty("timeSpent", out var ftEl) || el.TryGetProperty("time_spent", out ftEl))
                ? ftEl.GetDouble() / 60.0 : 0;

            DateTime? startTime = null;
            if ((el.TryGetProperty("startedAt", out var saEl) || el.TryGetProperty("started_at", out saEl))
                && saEl.ValueKind == JsonValueKind.String)
            {
                var saStr = saEl.GetString();
                if (!string.IsNullOrWhiteSpace(saStr)) startTime = DateTime.Parse(saStr);
            }

            DateTime? endTime = null;
            if ((el.TryGetProperty("completedAt", out var caEl) || el.TryGetProperty("completed_at", out caEl))
                && caEl.ValueKind == JsonValueKind.String)
            {
                var caStr = caEl.GetString();
                if (!string.IsNullOrWhiteSpace(caStr)) endTime = DateTime.Parse(caStr);
            }

            return new LiveTrackingFileModel
            {
                FileName = fname,
                FileStatus = fStatus,
                TimeSpent = fTime,
                StartTime = startTime,
                EndTime = endTime,
            };
        }

        private static List<LiveTrackingFileModel> SortFilesByStatus(List<LiveTrackingFileModel> files)
        {
            return files
                .OrderBy(f => f.IsWorkingFile ? 0 : 1)
                .ThenByDescending(f => f.StartTime ?? f.EndTime ?? DateTime.MinValue)
                .ToList();
        }

        private static DateTime ToUtcSafe(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc) return dateTime;
            if (dateTime.Kind == DateTimeKind.Local) return dateTime.ToUniversalTime();
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime();
        }

        private void OnTrackerUpdated(JsonElement data)
        {
            if (_allData == null) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var emp = data.TryGetProperty("employeeName", out var empEl) ? (empEl.GetString() ?? "") : "";
                    var shift = data.TryGetProperty("shift", out var shiftEl) ? (shiftEl.GetString() ?? "") : "";
                    var folder = data.TryGetProperty("folderPath", out var folderEl) ? (folderEl.GetString() ?? "") : "";
                    var workType = data.TryGetProperty("workType", out var workTypeEl) ? (workTypeEl.GetString() ?? "") : "";
                    var client = data.TryGetProperty("clientCode", out var clientEl) ? (clientEl.GetString() ?? "") : "";
                    var topFileName = (data.TryGetProperty("fileName", out var topFnEl) || data.TryGetProperty("file_name", out topFnEl))
                        ? (topFnEl.GetString() ?? "")
                        : "";
                    var updatedStatus = (data.TryGetProperty("fileStatus", out var fsEl) || data.TryGetProperty("file_status", out fsEl))
                        ? (fsEl.GetString() ?? "")
                        : "";
                    var updateTime = data.TryGetProperty("timestamp", out var tsEl) ? DateTime.Parse(tsEl.GetString()!) : DateTime.Now;

                    emp = (emp ?? string.Empty).Trim();
                    shift = (shift ?? string.Empty).Trim();
                    folder = (folder ?? string.Empty).Trim();
                    workType = (workType ?? string.Empty).Trim();
                    client = (client ?? string.Empty).Trim();
                    topFileName = (topFileName ?? string.Empty).Trim();

                    if (!string.IsNullOrWhiteSpace(emp) && !string.IsNullOrWhiteSpace(updatedStatus))
                    {
                        PauseTab.ApplyFileStatusUpdate(emp, updatedStatus, updateTime);
                    }

                    bool MatchesOrWildcard(string existing, string incoming)
                    {
                        var inc = (incoming ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(inc)) return true;
                        return string.Equals((existing ?? string.Empty).Trim(), inc, StringComparison.OrdinalIgnoreCase);
                    }

                    var session = _allData.FirstOrDefault(s =>
                        string.Equals((s.EmployeeName ?? string.Empty).Trim(), emp, StringComparison.OrdinalIgnoreCase)
                        && MatchesOrWildcard(s.Shift, shift)
                        && MatchesOrWildcard(s.FolderPath, folder)
                        && MatchesOrWildcard(s.WorkType, workType)
                        && MatchesOrWildcard(s.ClientCode, client));

                    if (session != null)
                    {
                        var previousFiles = session.Files != null
                            ? session.Files.Where(f => f != null).ToList()
                            : new List<LiveTrackingFileModel>();

                        var deltaSaysWorkingTop = LiveTrackingFileModel.IsWorkingStatus((updatedStatus ?? string.Empty).Trim());

                        // Do not overwrite known metadata with empty values from deltas.
                        if (!string.IsNullOrWhiteSpace(shift)) session.Shift = shift;
                        if (!string.IsNullOrWhiteSpace(folder)) session.FolderPath = folder;
                        if (!string.IsNullOrWhiteSpace(workType)) session.WorkType = workType;
                        if (!string.IsNullOrWhiteSpace(client)) session.ClientCode = client;

                        // Update session-level aggregates from delta (backend sends seconds)
                        if (data.TryGetProperty("total_times", out var ttEl)) session.TotalTimes = ttEl.GetDouble() / 60.0;
                        if (data.TryGetProperty("pause_time", out var ptEl)) session.PauseTime = ptEl.GetDouble() / 60.0;
                        if (data.TryGetProperty("pause_count", out var pcEl)) session.PauseCount = pcEl.GetInt32();
                        if (data.TryGetProperty("estimate_time", out var etEl)) session.EstimateTime = etEl.GetDouble();
                        session.UpdatedAt = updateTime;

                        // Update pause reasons if provided
                        if (data.TryGetProperty("pause_reasons", out var prArr) && prArr.ValueKind == JsonValueKind.Array)
                        {
                            var reasons = new List<string>();
                            var details = new List<PauseReasonItemModel>();
                            foreach (var pr in prArr.EnumerateArray())
                            {
                                var reason = pr.TryGetProperty("reason", out var rEl) ? rEl.GetString() ?? "" : "";
                                if (!string.IsNullOrWhiteSpace(reason))
                                {
                                    reasons.Add(reason.Trim());
                                    var item = new PauseReasonItemModel { Reason = reason.Trim() };
                                    if (pr.TryGetProperty("duration", out var dEl)) item.Duration = dEl.GetDouble() / 60.0;
                                    if (pr.TryGetProperty("started_at", out var sEl) && sEl.TryGetDateTime(out var sdt)) item.StartTime = sdt;
                                    if (pr.TryGetProperty("completed_at", out var cEl) && cEl.TryGetDateTime(out var cdt)) item.EndTime = cdt;
                                    details.Add(item);
                                }
                            }
                            if (reasons.Any()) session.PauseReasons = reasons;
                            if (details.Any()) session.PauseReasonDetails = details;
                        }

                        var hasFilesArray = data.TryGetProperty("files", out var filesArr) && filesArr.ValueKind == JsonValueKind.Array;
                        var hasNonEmptyFilesArray = hasFilesArray && filesArr.GetArrayLength() > 0;

                        if (hasFilesArray && !hasNonEmptyFilesArray)
                        {
                            // Some backend deltas can include an empty files[] array.
                            // Treat that as "files omitted"; never clear our last-known list because it will
                            // flip IsActive false and cause Working → Idle flicker.
                        }

                        if (hasNonEmptyFilesArray)
                        {
                            var newFiles = new List<LiveTrackingFileModel>();
                            var topStatus = (updatedStatus ?? string.Empty).Trim();
                            foreach (var updatedFile in filesArr.EnumerateArray())
                            {
                                var f = ParseFileFromJson(updatedFile);
                                // If file status missing, infer from previous state or top-level status
                                if (string.IsNullOrWhiteSpace(f.FileStatus) && !string.IsNullOrWhiteSpace(f.FileName))
                                {
                                    var prev = previousFiles.FirstOrDefault(pf => string.Equals(pf.FileName, f.FileName, StringComparison.OrdinalIgnoreCase));
                                    if (prev != null)
                                    {
                                        var prevStatus = (prev.FileStatus ?? string.Empty).Trim();
                                        var topIsPaused = string.Equals(topStatus, "pause", StringComparison.OrdinalIgnoreCase)
                                            || string.Equals(topStatus, "paused", StringComparison.OrdinalIgnoreCase);

                                        if (LiveTrackingFileModel.IsWorkingStatus(prevStatus)
                                            && (topIsPaused || string.Equals(topStatus, "done", StringComparison.OrdinalIgnoreCase)))
                                            f.FileStatus = topStatus;
                                        else
                                            f.FileStatus = prevStatus;
                                    }
                                }
                                newFiles.Add(f);
                            }

                            var sorted = SortFilesByStatus(newFiles);

                            var wasWorking = previousFiles.Any(f => f.IsWorkingFile);
                            var nowWorking = sorted.Any(f => f.IsWorkingFile);
                            var deltaSaysWorking = LiveTrackingFileModel.IsWorkingStatus((updatedStatus ?? string.Empty).Trim());

                            // If the delta indicates the user is working but the files array doesn't include any working file,
                            // ensure we still have at least one working file in the session so IsActive stays true.
                            // This prevents the user from disappearing from Client/Production/QC tabs while Activity still shows them.
                            if (deltaSaysWorking && !nowWorking)
                            {
                                LiveTrackingFileModel? target = null;
                                if (!string.IsNullOrWhiteSpace(topFileName))
                                {
                                    target = sorted.FirstOrDefault(f => string.Equals((f.FileName ?? string.Empty).Trim(), topFileName, StringComparison.OrdinalIgnoreCase));
                                }

                                if (target == null)
                                {
                                    target = sorted
                                        .OrderByDescending(f => f.StartTime ?? f.EndTime ?? DateTime.MinValue)
                                        .FirstOrDefault();
                                }

                                if (target == null)
                                {
                                    target = new LiveTrackingFileModel
                                    {
                                        FileName = topFileName,
                                        FileStatus = (updatedStatus ?? string.Empty).Trim(),
                                        TimeSpent = 0,
                                        StartTime = updateTime,
                                        EndTime = null,
                                    };
                                    sorted.Add(target);
                                }

                                if (target != null)
                                {
                                    target.FileStatus = (updatedStatus ?? string.Empty).Trim();
                                }

                                nowWorking = true;
                            }

                            // Defensive: some deltas arrive with a files list that temporarily omits the active working file.
                            // If this update would flip an active session to inactive while the delta status indicates working,
                            // keep the last known file list so the UI doesn't drop the user from live tabs.
                            if (wasWorking && !nowWorking && deltaSaysWorking)
                            {
                                ApplyGlobalFilters();
                                return;
                            }

                            if (session.Files != null)
                            {
                                session.Files.Clear();
                                foreach (var f in sorted) session.Files.Add(f);
                                session.NotifyFilesChanged();
                            }
                            ApplyGlobalFilters();
                        }
                        else
                        {
                            // If server omitted files[] in delta, keep session active based on top-level status.
                            if (deltaSaysWorkingTop && session.Files != null)
                            {
                                var hasWorking = session.Files.Any(f => f.IsWorkingFile);
                                if (!hasWorking)
                                {
                                    LiveTrackingFileModel? existing = null;
                                    if (!string.IsNullOrWhiteSpace(topFileName))
                                    {
                                        existing = session.Files.FirstOrDefault(f => string.Equals((f.FileName ?? string.Empty).Trim(), topFileName, StringComparison.OrdinalIgnoreCase));
                                    }
                                    if (existing == null)
                                    {
                                        existing = session.Files.OrderByDescending(f => f.StartTime ?? f.EndTime ?? DateTime.MinValue).FirstOrDefault();
                                    }

                                    if (existing == null)
                                    {
                                        existing = new LiveTrackingFileModel
                                        {
                                            FileName = topFileName,
                                            FileStatus = (updatedStatus ?? string.Empty).Trim(),
                                            TimeSpent = 0,
                                            StartTime = updateTime,
                                            EndTime = null,
                                        };
                                        session.Files.Add(existing);
                                    }
                                    else
                                    {
                                        existing.FileStatus = (updatedStatus ?? string.Empty).Trim();
                                    }

                                    session.NotifyFilesChanged();
                                }
                            }

                            // Even if no files array, aggregates changed so refresh UI
                            ApplyGlobalFilters();
                        }
                    }
                    else
                    {
                        var newSession = new LiveTrackingSessionModel
                        {
                            Id = data.TryGetProperty("_id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                            EmployeeName = emp,
                            Shift = shift,
                            FolderPath = folder,
                            WorkType = workType,
                            ClientCode = client,
                            Categories = data.TryGetProperty("categories", out var catEl) ? catEl.GetString() ?? "" : "",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                        };

                        // Populate session-level aggregates (backend sends seconds)
                        if (data.TryGetProperty("total_times", out var ttEl2)) newSession.TotalTimes = ttEl2.GetDouble() / 60.0;
                        if (data.TryGetProperty("pause_time", out var ptEl2)) newSession.PauseTime = ptEl2.GetDouble() / 60.0;
                        if (data.TryGetProperty("pause_count", out var pcEl2)) newSession.PauseCount = pcEl2.GetInt32();
                        if (data.TryGetProperty("estimate_time", out var etEl2)) newSession.EstimateTime = etEl2.GetDouble();

                        if (data.TryGetProperty("files", out var filesArr) && filesArr.ValueKind == JsonValueKind.Array)
                        {
                            var newFiles = new List<LiveTrackingFileModel>();
                            foreach (var updatedFile in filesArr.EnumerateArray())
                            {
                                var f = ParseFileFromJson(updatedFile);
                                if (string.IsNullOrWhiteSpace(f.FileStatus))
                                {
                                    var top = (updatedStatus ?? string.Empty).Trim();
                                    if (LiveTrackingFileModel.IsWorkingStatus(top))
                                        f.FileStatus = top;
                                }
                                newFiles.Add(f);
                            }

                            foreach (var f in SortFilesByStatus(newFiles))
                                newSession.Files.Add(f);
                        }

                        _allData.Add(newSession);
                        ApplyGlobalFilters();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LiveTrackingViewModel] OnTrackerUpdated Error: {ex}");
                }
            });
        }

        private void OnReportUpdated(JsonElement data)
        {
            if (_allData == null) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var emp = data.TryGetProperty("employeeName", out var empEl) ? empEl.GetString() ?? "" : "";
                    var fname = data.TryGetProperty("fileName", out var fnEl) ? fnEl.GetString() ?? "" : "";
                    var report = data.TryGetProperty("report", out var repEl) ? repEl.GetString() ?? "" : "";

                    foreach (var s in _allData.Where(x => string.Equals(x.EmployeeName, emp, StringComparison.OrdinalIgnoreCase)))
                    {
                        var file = s.Files.FirstOrDefault(f => string.Equals(f.FileName, fname, StringComparison.OrdinalIgnoreCase));
                        if (file != null)
                        {
                            file.Report = report;
                            ApplyGlobalFilters();
                            // Only one file per day with same name for employee (usually).
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LiveTrackingViewModel] OnReportUpdated Error: {ex}");
                }
            });
        }

        private void OnSessionUpdated(JsonElement data)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var username = data.TryGetProperty("username", out var uEl) ? uEl.GetString() ?? "" : "";
                    DateTime? loginAt = null;
                    DateTime? logoutAt = null;

                    if (data.TryGetProperty("loginAt", out var laEl))
                    {
                        if (laEl.ValueKind == JsonValueKind.String)
                        {
                            var s = laEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) loginAt = DateTime.Parse(s);
                        }
                    }
                    if (data.TryGetProperty("logoutAt", out var loEl))
                    {
                        if (loEl.ValueKind == JsonValueKind.String)
                        {
                            var s = loEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) logoutAt = DateTime.Parse(s);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        PauseTab.ApplySessionUpdate(username, loginAt, logoutAt);

                        var idx = _allSessionData.FindIndex(s => string.Equals(s.Username, username, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            var existing = _allSessionData[idx];
                            var mergedFirst = existing.FirstLoginAt;
                            if (loginAt.HasValue)
                            {
                                mergedFirst = !mergedFirst.HasValue
                                    ? loginAt
                                    : (loginAt.Value < mergedFirst.Value ? loginAt : mergedFirst);
                            }

                            _allSessionData[idx] = new TrackerUserSessionModel
                            {
                                Username = existing.Username,
                                FirstLoginAt = mergedFirst,
                                LastLogoutAt = logoutAt,
                                IsActive = !logoutAt.HasValue,
                                TotalDurationSeconds = existing.TotalDurationSeconds,
                            };
                        }
                        else
                        {
                            _allSessionData.Add(new TrackerUserSessionModel
                            {
                                Username = username,
                                FirstLoginAt = loginAt,
                                LastLogoutAt = logoutAt,
                                IsActive = !logoutAt.HasValue,
                                TotalDurationSeconds = 0,
                            });
                        }
                    }

                    // No full reload here; only update derived UI.
                    ApplyGlobalFilters();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LiveTrackingViewModel] OnSessionUpdated Error: {ex}");
                }
            });
        }

        public async Task LoadDataAsync()
        {
            // Use a semaphore so at most one load runs at a time.
            // If a load is already in progress, skip this tick to avoid flooding the server.
            if (!_loadLock.Wait(0)) return;

            try
            {
                IsLoading = true;

                var previousWorkLogs = _allData;
                var previousSessions = _allSessionData;

                static string KeyOf(LiveTrackingSessionModel s)
                {
                    if (s == null) return string.Empty;
                    return string.Join("|||",
                        (s.EmployeeName ?? string.Empty).Trim().ToLowerInvariant(),
                        (s.Shift ?? string.Empty).Trim().ToLowerInvariant(),
                        (s.FolderPath ?? string.Empty).Trim().ToLowerInvariant(),
                        (s.WorkType ?? string.Empty).Trim().ToLowerInvariant(),
                        (s.ClientCode ?? string.Empty).Trim().ToLowerInvariant());
                }

                void PreserveRecentActiveSessions(System.Collections.Generic.List<LiveTrackingSessionModel> incoming)
                {
                    if (incoming == null || incoming.Count == 0) return;
                    if (previousWorkLogs == null || previousWorkLogs.Count == 0) return;

                    var prevByKey = previousWorkLogs
                        .Where(s => s != null)
                        .GroupBy(KeyOf)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

                    var nowUtc = DateTime.UtcNow;

                    foreach (var inc in incoming)
                    {
                        if (inc == null) continue;
                        if (inc.IsActive) continue;

                        var key = KeyOf(inc);
                        if (string.IsNullOrWhiteSpace(key)) continue;

                        if (!prevByKey.TryGetValue(key, out var prev) || prev == null) continue;
                        if (!prev.IsActive) continue;

                        var prevUpdatedUtc = ToUtcSafe(prev.UpdatedAt);
                        if ((nowUtc - prevUpdatedUtc) > TimeSpan.FromSeconds(60)) continue;

                        if (ToUtcSafe(inc.UpdatedAt) >= prevUpdatedUtc) continue;

                        inc.EmployeeName = prev.EmployeeName;
                        inc.Shift = prev.Shift;
                        inc.FolderPath = prev.FolderPath;
                        inc.WorkType = prev.WorkType;
                        inc.ClientCode = prev.ClientCode;
                        inc.Categories = prev.Categories;
                        inc.EstimateTime = prev.EstimateTime;
                        inc.TotalTimes = prev.TotalTimes;
                        inc.PauseCount = prev.PauseCount;
                        inc.PauseTime = prev.PauseTime;
                        inc.PauseReasons = prev.PauseReasons;

                        inc.Files.Clear();
                        foreach (var f in prev.Files) inc.Files.Add(f);
                        inc.NotifyFilesChanged();
                    }
                }

                static List<LiveTrackingSessionModel> MergeWorkLogs(
                    List<LiveTrackingSessionModel>? previous,
                    List<LiveTrackingSessionModel> incoming,
                    Func<LiveTrackingSessionModel, string> keyOf,
                    Func<DateTime, DateTime> toUtcSafe)
                {
                    var result = new List<LiveTrackingSessionModel>();
                    var prevList = previous ?? new List<LiveTrackingSessionModel>();
                    var prevByKey = prevList
                        .Where(s => s != null)
                        .GroupBy(keyOf)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

                    var nowUtc = DateTime.UtcNow;

                    foreach (var inc in incoming.Where(s => s != null))
                    {
                        var key = keyOf(inc);
                        if (!string.IsNullOrWhiteSpace(key) && prevByKey.TryGetValue(key, out var prev) && prev != null)
                        {
                            var incUpdatedUtc = toUtcSafe(inc.UpdatedAt);
                            var prevUpdatedUtc = toUtcSafe(prev.UpdatedAt);

                            if (prevUpdatedUtc > incUpdatedUtc)
                            {
                                result.Add(prev);
                            }
                            else
                            {
                                result.Add(inc);
                            }

                            prevByKey.Remove(key);
                        }
                        else
                        {
                            result.Add(inc);
                        }
                    }

                    foreach (var kv in prevByKey)
                    {
                        var prev = kv.Value;
                        if (prev == null) continue;

                        var prevUpdatedUtc = toUtcSafe(prev.UpdatedAt);
                        if (prev.IsActive || (nowUtc - prevUpdatedUtc) <= TimeSpan.FromMinutes(2))
                        {
                            result.Add(prev);
                        }
                    }

                    return result;
                }

                LiveTrackingSnapshot? snapshot;
                if (DateFrom.HasValue && DateTo.HasValue)
                {
                    var from = DateFrom.Value.ToString("yyyy-MM-dd");
                    var to = DateTo.Value.ToString("yyyy-MM-dd");
                    snapshot = await _dataService.GetLiveTrackingDataRangeAsync(from, to);
                }
                else
                {
                    var selectedDate = SelectedDate.ToString("yyyy-MM-dd");
                    snapshot = await _dataService.GetLiveTrackingDataAsync(selectedDate);
                }

                var incomingWorkLogs = snapshot?.WorkLogs ?? new List<LiveTrackingSessionModel>();
                var incomingSessions = snapshot?.Sessions?.Where(s => s != null).ToList() ?? new List<TrackerUserSessionModel>();

                if (incomingWorkLogs.Count == 0 && previousWorkLogs != null && previousWorkLogs.Count > 0)
                {
                    Debug.WriteLine("[LiveTrackingViewModel] Snapshot reload returned 0 worklogs; keeping previous.");
                    _allData = previousWorkLogs;
                    _allSessionData = previousSessions ?? new List<TrackerUserSessionModel>();
                }
                else
                {
                    PreserveRecentActiveSessions(incomingWorkLogs);
                    _allData = MergeWorkLogs(previousWorkLogs, incomingWorkLogs, KeyOf, ToUtcSafe);
                    _allSessionData = incomingSessions;

                    PauseTab.ResetSessionSnapshot();
                    if (snapshot?.Sessions != null)
                    {
                        foreach (var s in snapshot.Sessions)
                        {
                            if (s == null) continue;
                            PauseTab.ApplySessionUpdate(
                                s.Username,
                                s.FirstLoginAt,
                                s.IsActive ? null : s.LastLogoutAt,
                                s.TotalDurationSeconds
                            );
                        }
                    }
                }

                // Save current selections before rebuilding lists
                var savedClient = _selectedClient;
                var savedUser = _selectedUser;
                var savedShift = _selectedShift;

                // Populate filter dropdowns
                var clients = _allData
                    .Where(s => !string.IsNullOrWhiteSpace(s.ClientCode))
                    .Select(s => s.ClientCode.Trim())
                    .Distinct().OrderBy(c => c).ToList();
                _availableClients.Clear();
                _availableClients.Add("All Clients");
                foreach (var c in clients) _availableClients.Add(c);

                var users = _allData
                    .Where(s => !string.IsNullOrWhiteSpace(s.EmployeeName))
                    .Select(s => s.EmployeeName.Trim())
                    .Distinct().OrderBy(u => u).ToList();
                _availableUsers.Clear();
                _availableUsers.Add("All Users");
                foreach (var u in users) _availableUsers.Add(u);

                // Shift list: keep fixed 3 shifts + any shift values from live data
                var shifts = _allData
                    .Where(s => !string.IsNullOrWhiteSpace(s.Shift))
                    .Select(s => s.Shift.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s)
                    .ToList();
                _availableShifts.Clear();
                _availableShifts.Add("All Shifts");

                void AddShiftIfMissing(string shift)
                {
                    if (string.IsNullOrWhiteSpace(shift)) return;
                    if (!_availableShifts.Any(x => string.Equals(x, shift, StringComparison.OrdinalIgnoreCase)))
                    {
                        _availableShifts.Add(shift);
                    }
                }

                AddShiftIfMissing("Morning");
                AddShiftIfMissing("Evening");
                AddShiftIfMissing("Night");
                foreach (var s in shifts) AddShiftIfMissing(s);

                // Restore selections (if still valid), without triggering reload
                _selectedClient = _availableClients.Contains(savedClient) ? savedClient : "All Clients";
                OnPropertyChanged(nameof(SelectedClient));
                _selectedUser = _availableUsers.Contains(savedUser) ? savedUser : "All Users";
                OnPropertyChanged(nameof(SelectedUser));

                if (_availableShifts.Any(s => string.Equals(s, savedShift, StringComparison.OrdinalIgnoreCase))
                    && !string.Equals(savedShift, "All Shifts", StringComparison.OrdinalIgnoreCase))
                {
                    _selectedShift = savedShift;
                }
                else
                {
                    _selectedShift = "All Shifts";
                }
                OnPropertyChanged(nameof(SelectedShift));

                ApplyGlobalFilters();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiveTrackingViewModel] Data load error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _loadLock.Release();
            }
        }

        private void ApplyGlobalFilters()
        {
            if (_allData == null) return;

            var filtered = _allData.AsEnumerable();

            if (!string.Equals(SelectedClient, "All Clients", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(SelectedClient))
                filtered = filtered.Where(s => string.Equals(s.ClientCode?.Trim(), SelectedClient.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.Equals(SelectedUser, "All Users", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(SelectedUser))
                filtered = filtered.Where(s => string.Equals(s.EmployeeName?.Trim(), SelectedUser.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!string.Equals(SelectedShift, "All Shifts", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(SelectedShift))
                filtered = filtered.Where(s => string.Equals(s.Shift?.Trim(), SelectedShift.Trim(), StringComparison.OrdinalIgnoreCase));

            var filteredList = filtered.ToList();

            var filteredSessions = _allSessionData ?? new List<TrackerUserSessionModel>();
            if (!string.Equals(SelectedUser, "All Users", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(SelectedUser))
            {
                filteredSessions = filteredSessions
                    .Where(s => s != null
                        && string.Equals((s.Username ?? string.Empty).Trim(), SelectedUser.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ClientTab.RefreshData(filteredList);
            ProductionTab.RefreshData(filteredList);
            QcTab.RefreshData(filteredList);
            PauseTab.RefreshData(filteredList);
            IdleTab.RefreshData(
                filteredList,
                filteredSessions,
                SelectedShift,
                ToDisplayShift(SCHLStudio.App.Services.Api.Tracker.ShiftDetector.GetCurrentShift())
            );
            ProductivityTab.RefreshData(filteredList);
            UserSummaryTab.RefreshData(filteredList, filteredSessions);
        }

        private static string ToDisplayShift(string shift)
        {
            var s = (shift ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(s)) return "All Shifts";
            if (string.Equals(s, "morning", StringComparison.OrdinalIgnoreCase)) return "Morning";
            if (string.Equals(s, "evening", StringComparison.OrdinalIgnoreCase)) return "Evening";
            if (string.Equals(s, "night", StringComparison.OrdinalIgnoreCase)) return "Night";
            return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
        }
    }
}
