using System;
using System.Collections.Generic;
using System.Globalization;
using SCHLStudio.App.ViewModels.Base;
using System.Collections.ObjectModel;

namespace SCHLStudio.App.ViewModels.LiveTracking.Models
{
    public class LiveTrackingFileModel : ViewModelBase
    {
        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            set
            {
                if (SetProperty(ref _fileName, value))
                    OnPropertyChanged(nameof(FileNameDisplay));
            }
        }

        public string FileNameDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(_fileName) || _fileName.Length <= 42) return _fileName;
                return _fileName.Substring(0, 20) + "..." + _fileName.Substring(_fileName.Length - 20);
            }
        }

        private string _fileStatus = string.Empty;
        public string FileStatus
        {
            get => _fileStatus;
            set
            {
                if (SetProperty(ref _fileStatus, value))
                {
                    OnPropertyChanged(nameof(FileStatusDisplay));
                }
            }
        }

        private string _report = string.Empty;
        public string Report { get => _report; set => SetProperty(ref _report, value); }

        private double _timeSpent;
        public double TimeSpent
        {
            get => _timeSpent;
            set
            {
                if (SetProperty(ref _timeSpent, value))
                {
                    OnPropertyChanged(nameof(TimeSpentFormatted));
                }
            }
        }

        private DateTime? _startTime;
        public DateTime? StartTime
        {
            get => _startTime;
            set
            {
                if (SetProperty(ref _startTime, value))
                {
                    OnPropertyChanged(nameof(StartTimeFormatted));
                }
            }
        }

        private DateTime? _endTime;
        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (SetProperty(ref _endTime, value))
                {
                    OnPropertyChanged(nameof(EndTimeFormatted));
                }
            }
        }

        public string FileStatusDisplay =>
            string.IsNullOrEmpty(FileStatus) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(FileStatus.ToLower());

        public string TimeSpentFormatted => FormatMinutes(TimeSpent);

        public string StartTimeFormatted =>
            StartTime.HasValue ? StartTime.Value.ToLocalTime().ToString("hh:mm tt") : "—";

        public string EndTimeFormatted =>
            EndTime.HasValue ? EndTime.Value.ToLocalTime().ToString("hh:mm tt") : "—";

        public void NotifyLiveTick()
        {
            OnPropertyChanged(nameof(TimeSpentFormatted));
        }

        internal static string FormatMinutes(double totalMinutes)
        {
            if (totalMinutes <= 0) return "0m";
            int hours = (int)(totalMinutes / 60);
            int mins = (int)(totalMinutes % 60);
            int secs = (int)((totalMinutes * 60) % 60);
            if (hours > 0 && mins > 0) return $"{hours}h {mins}m";
            if (hours > 0) return $"{hours}h";
            if (mins > 0) return $"{mins}m";
            return $"{secs}s";
        }
    }

    public class LiveTrackingSessionModel : ViewModelBase
    {
        private string _id = string.Empty;
        public string Id { get => _id; set => SetProperty(ref _id, value); }

        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

        private string _employeeName = string.Empty;
        public string EmployeeName
        {
            get => _employeeName;
            set
            {
                if (SetProperty(ref _employeeName, value))
                    OnPropertyChanged(nameof(EmployeeNameDisplay));
            }
        }

        private string _shift = string.Empty;
        public string Shift
        {
            get => _shift;
            set
            {
                if (SetProperty(ref _shift, value))
                    OnPropertyChanged(nameof(ShiftDisplay));
            }
        }

        private string _folderPath = string.Empty;
        public string FolderPath { get => _folderPath; set => SetProperty(ref _folderPath, value); }

        private string _workType = string.Empty;
        public string WorkType
        {
            get => _workType;
            set
            {
                if (SetProperty(ref _workType, value))
                    OnPropertyChanged(nameof(WorkTypeDisplay));
            }
        }

        private string _clientCode = string.Empty;
        public string ClientCode { get => _clientCode; set => SetProperty(ref _clientCode, value); }

        private string _categories = string.Empty;
        public string Categories
        {
            get => _categories;
            set
            {
                if (SetProperty(ref _categories, value))
                    OnPropertyChanged(nameof(CategoriesDisplay));
            }
        }

        public string CategoriesDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_categories)) return "—";
                var idx = _categories.IndexOf(',');
                return idx > 0 ? _categories.Substring(0, idx).Trim() : _categories.Trim();
            }
        }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (SetProperty(ref _createdAt, value))
                    OnPropertyChanged(nameof(StartTimeFormatted));
            }
        }

        private DateTime _updatedAt;
        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (SetProperty(ref _updatedAt, value))
                    OnPropertyChanged(nameof(EndTimeFormatted));
            }
        }

        private double _estimateTime;
        public double EstimateTime { get => _estimateTime; set => SetProperty(ref _estimateTime, value); }

        private double _totalTimes;
        public double TotalTimes
        {
            get => _totalTimes;
            set
            {
                if (SetProperty(ref _totalTimes, value))
                {
                    OnPropertyChanged(nameof(TotalTimesFormatted));
                    OnPropertyChanged(nameof(AvgTimePerFile));
                }
            }
        }

        private int _pauseCount;
        public int PauseCount { get => _pauseCount; set => SetProperty(ref _pauseCount, value); }

        private double _pauseTime;
        public double PauseTime
        {
            get => _pauseTime;
            set
            {
                if (SetProperty(ref _pauseTime, value))
                    OnPropertyChanged(nameof(PauseTimeFormatted));
            }
        }

        private List<string> _pauseReasons = new List<string>();
        public List<string> PauseReasons
        {
            get => _pauseReasons;
            set
            {
                if (SetProperty(ref _pauseReasons, value))
                    OnPropertyChanged(nameof(LatestPauseReason));
            }
        }

        private ObservableCollection<LiveTrackingFileModel> _files = new ObservableCollection<LiveTrackingFileModel>();
        public ObservableCollection<LiveTrackingFileModel> Files
        {
            get => _files;
            set
            {
                if (SetProperty(ref _files, value))
                {
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(AvgTimePerFile));
                    OnPropertyChanged(nameof(CurrentFileName));
                }
            }
        }

        public void NotifyFilesChanged()
        {
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(AvgTimePerFile));
            OnPropertyChanged(nameof(CurrentFileName));
            OnPropertyChanged(nameof(IsActive));
        }

        public string EmployeeNameDisplay =>
            string.IsNullOrEmpty(EmployeeName) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(EmployeeName.ToLower());

        public string ShiftDisplay =>
            string.IsNullOrEmpty(Shift) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Shift.ToLower());

        public string WorkTypeDisplay =>
            string.IsNullOrEmpty(WorkType) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(WorkType.ToLower());

        public string Progress
        {
            get
            {
                if (Files == null) return "0 / 0";
                int done = 0;
                foreach (var f in Files)
                    if (string.Equals(f.FileStatus, "done", StringComparison.OrdinalIgnoreCase))
                        done++;
                return $"{done} / {Files.Count}";
            }
        }

        public string TotalTimesFormatted => LiveTrackingFileModel.FormatMinutes(TotalTimes);
        public string PauseTimeFormatted => LiveTrackingFileModel.FormatMinutes(PauseTime);

        public string AvgTimePerFile
        {
            get
            {
                if (Files == null || Files.Count == 0) return "—";
                return LiveTrackingFileModel.FormatMinutes(TotalTimes / Files.Count);
            }
        }

        public string StartTimeFormatted =>
            CreatedAt != default ? CreatedAt.ToLocalTime().ToString("hh:mm tt") : "—";

        public string EndTimeFormatted =>
            UpdatedAt != default ? UpdatedAt.ToLocalTime().ToString("hh:mm tt") : "—";

        public string CurrentFileName
        {
            get
            {
                if (Files == null) return "—";
                foreach (var f in Files)
                    if (string.Equals(f.FileStatus, "working", StringComparison.OrdinalIgnoreCase))
                        return f.FileName;
                return Files.Count > 0 ? Files[Files.Count - 1].FileName : "—";
            }
        }

        public bool IsActive
        {
            get
            {
                if (Files == null || Files.Count == 0) return false;

                // Important: determine activity by presence of ANY currently working file.
                // Using the "latest" file by timestamps can be wrong because a recently completed
                // (done) file may have a newer EndTime than the current working file's StartTime,
                // causing the session to be treated as inactive and disappear from live tabs.
                foreach (var f in Files)
                {
                    var status = (f?.FileStatus ?? string.Empty).Trim();
                    if (IsWorkingStatus(status))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static bool IsWorkingStatus(string status)
        {
            return string.Equals(status, "working", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "in progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "in-progress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "inprogress", StringComparison.OrdinalIgnoreCase);
        }

        public string LatestPauseReason =>
            PauseReasons != null && PauseReasons.Count > 0
                ? PauseReasons[PauseReasons.Count - 1]
                : "—";

        public void NotifyLiveTick()
        {
            try
            {
                if (Files != null)
                {
                    foreach (var f in Files)
                    {
                        try
                        {
                            f?.NotifyLiveTick();
                        }
                        catch
                        {
                        }
                    }
                }

                OnPropertyChanged(nameof(TotalTimesFormatted));
                OnPropertyChanged(nameof(AvgTimePerFile));
                OnPropertyChanged(nameof(CurrentFileName));
                OnPropertyChanged(nameof(IsActive));
            }
            catch
            {
            }
        }
    }

    // --- Client Tab ---

    public class ClientEmployeeModel : ViewModelBase
    {
        private string _employeeName = string.Empty;
        public string EmployeeName { get => _employeeName; set => SetProperty(ref _employeeName, value); }
        
        private string _workType = string.Empty;
        public string WorkType { get => _workType; set => SetProperty(ref _workType, value); }
        
        private int _totalFiles;
        public int TotalFiles { get => _totalFiles; set => SetProperty(ref _totalFiles, value); }
        
        private int _completedFiles;
        public int CompletedFiles
        {
            get => _completedFiles;
            set
            {
                if (SetProperty(ref _completedFiles, value))
                    OnPropertyChanged(nameof(AvgTimeFormatted));
            }
        }
        
        private double _totalTime;
        public double TotalTime
        {
            get => _totalTime;
            set
            {
                if (SetProperty(ref _totalTime, value))
                {
                    OnPropertyChanged(nameof(TotalTimeFormatted));
                    OnPropertyChanged(nameof(AvgTimeFormatted));
                }
            }
        }

        public string EmployeeNameDisplay =>
            string.IsNullOrEmpty(EmployeeName) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(EmployeeName.ToLower());
        public string WorkTypeDisplay =>
            string.IsNullOrEmpty(WorkType) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(WorkType.ToLower());
        public string TotalTimeFormatted => LiveTrackingFileModel.FormatMinutes(TotalTime);
        public string AvgTimeFormatted =>
            CompletedFiles > 0 ? LiveTrackingFileModel.FormatMinutes(TotalTime / CompletedFiles) : "—";
    }

    public class ClientTabRowModel : ViewModelBase
    {
        private string _clientName = string.Empty;
        public string ClientName { get => _clientName; set => SetProperty(ref _clientName, value); }
        
        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
        
        private int _activeEmployees;
        public int ActiveEmployees { get => _activeEmployees; set => SetProperty(ref _activeEmployees, value); }

        private bool _isActive;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        
        private string _categories = string.Empty;
        public string Categories
        {
            get => _categories;
            set
            {
                if (SetProperty(ref _categories, value))
                {
                    OnPropertyChanged(nameof(CategoriesDisplay));
                }
            }
        }

        public string CategoriesDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_categories)) return "—";
                var idx = _categories.IndexOf(',');
                return idx > 0 ? _categories.Substring(0, idx).Trim() : _categories.Trim();
            }
        }
        
        private int _totalProductionFilesDone;
        public int TotalProductionFilesDone { get => _totalProductionFilesDone; set => SetProperty(ref _totalProductionFilesDone, value); }
        
        private int _totalQcFilesDone;
        public int TotalQcFilesDone { get => _totalQcFilesDone; set => SetProperty(ref _totalQcFilesDone, value); }
        
        private double _estimateTime;
        public double EstimateTime
        {
            get => _estimateTime;
            set
            {
                if (SetProperty(ref _estimateTime, value))
                    OnPropertyChanged(nameof(EstimateTimeFormatted));
            }
        }
        
        private double _totalTimeSpent;
        public double TotalTimeSpent
        {
            get => _totalTimeSpent;
            set
            {
                if (SetProperty(ref _totalTimeSpent, value))
                    OnPropertyChanged(nameof(TotalTimeSpentFormatted));
            }
        }
        
        private DateTime _startTime;
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (SetProperty(ref _startTime, value))
                    OnPropertyChanged(nameof(StartTimeFormatted));
            }
        }
        
        private DateTime _endTime;
        public DateTime EndTime
        {
            get => _endTime;
            set
            {
                if (SetProperty(ref _endTime, value))
                    OnPropertyChanged(nameof(EndTimeFormatted));
            }
        }

        private ObservableCollection<ClientEmployeeModel> _employees = new ObservableCollection<ClientEmployeeModel>();
        public ObservableCollection<ClientEmployeeModel> Employees { get => _employees; set => SetProperty(ref _employees, value); }

        public string ClientNameDisplay =>
            string.IsNullOrEmpty(ClientName) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(ClientName.ToLower());
        public string EstimateTimeFormatted => LiveTrackingFileModel.FormatMinutes(EstimateTime);
        public string TotalTimeSpentFormatted => LiveTrackingFileModel.FormatMinutes(TotalTimeSpent);
        public string AvgTimeFormatted
        {
            get
            {
                var totalDone = TotalProductionFilesDone + TotalQcFilesDone;
                if (totalDone <= 0) return "—";
                return LiveTrackingFileModel.FormatMinutes(TotalTimeSpent / totalDone);
            }
        }
        public string StartTimeFormatted =>
            StartTime != default ? StartTime.ToLocalTime().ToString("hh:mm tt") : "—";
        public string EndTimeFormatted =>
            EndTime != default ? EndTime.ToLocalTime().ToString("hh:mm tt") : "—";
    }

    // --- Productivity Tab ---

    public class ProductivityUserModel : ViewModelBase
    {
        private string _employeeName = string.Empty;
        public string EmployeeName { get => _employeeName; set => SetProperty(ref _employeeName, value); }
        
        private string _workType = string.Empty;
        public string WorkType { get => _workType; set => SetProperty(ref _workType, value); }
        
        private int _totalFiles;
        public int TotalFiles { get => _totalFiles; set => SetProperty(ref _totalFiles, value); }
        
        private int _completedFiles;
        public int CompletedFiles
        {
            get => _completedFiles;
            set
            {
                if (SetProperty(ref _completedFiles, value))
                    OnPropertyChanged(nameof(AvgTimeFormatted));
            }
        }
        
        private double _totalTime;
        public double TotalTime
        {
            get => _totalTime;
            set
            {
                if (SetProperty(ref _totalTime, value))
                {
                    OnPropertyChanged(nameof(TotalTimeFormatted));
                    OnPropertyChanged(nameof(AvgTimeFormatted));
                }
            }
        }

        public string EmployeeNameDisplay =>
            string.IsNullOrEmpty(EmployeeName) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(EmployeeName.ToLower());
        public string WorkTypeDisplay =>
            string.IsNullOrEmpty(WorkType) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(WorkType.ToLower());
        public string TotalTimeFormatted => LiveTrackingFileModel.FormatMinutes(TotalTime);
        public string AvgTimeFormatted =>
            CompletedFiles > 0 ? LiveTrackingFileModel.FormatMinutes(TotalTime / CompletedFiles) : "—";
    }

    /// <summary>Category group with employees under that category</summary>
    public class CategoryEmployeeGroupModel : ViewModelBase
    {
        private string _category = string.Empty;
        public string Category { get => _category; set => SetProperty(ref _category, value); }
        
        private ObservableCollection<ProductivityUserModel> _employees = new ObservableCollection<ProductivityUserModel>();
        public ObservableCollection<ProductivityUserModel> Employees { get => _employees; set => SetProperty(ref _employees, value); }
        
        public string CategoryDisplay =>
            string.IsNullOrEmpty(Category) ? "—" : Category.Trim().ToUpperInvariant();
    }

    // --- Pause Tab ---

    public class PauseDetailModel : ViewModelBase
    {
        private List<string> _pauseReasons = new List<string>();
        public List<string> PauseReasons
        {
            get => _pauseReasons;
            set
            {
                if (SetProperty(ref _pauseReasons, value))
                    OnPropertyChanged(nameof(PauseReasonsArrayText));
            }
        }

        public string PauseReasonsArrayText =>
            PauseReasons != null && PauseReasons.Count > 0
                ? $"[{string.Join(", ", PauseReasons)}]"
                : "[]";

        private string _reason = string.Empty;
        public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
        
        private string _clientCode = string.Empty;
        public string ClientCode { get => _clientCode; set => SetProperty(ref _clientCode, value); }
        
        private string _workType = string.Empty;
        public string WorkType { get => _workType; set => SetProperty(ref _workType, value); }
        
        private int _pauseCount;
        public int PauseCount { get => _pauseCount; set => SetProperty(ref _pauseCount, value); }

        
        private DateTime _startTime;
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (SetProperty(ref _startTime, value))
                    OnPropertyChanged(nameof(StartTimeFormatted));
            }
        }
        
        private DateTime? _endTime;
        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (SetProperty(ref _endTime, value))
                    OnPropertyChanged(nameof(EndTimeFormatted));
            }
        }
        
        private double _duration;
        public double Duration
        {
            get => _duration;
            set
            {
                if (SetProperty(ref _duration, value))
                    OnPropertyChanged(nameof(DurationFormatted));
            }
        }

        public string StartTimeFormatted =>
            StartTime != default ? StartTime.ToLocalTime().ToString("hh:mm tt") : "—";
            
        public string EndTimeFormatted =>
            EndTime.HasValue ? EndTime.Value.ToLocalTime().ToString("hh:mm tt") : "—";

        public string DurationFormatted => LiveTrackingFileModel.FormatMinutes(Duration);
    }

    public class PauseUserGroupModel : ViewModelBase
    {
        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
        
        private string _employeeName = string.Empty;
        public string EmployeeName { get => _employeeName; set => SetProperty(ref _employeeName, value); }
        
        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }
        
        private int _pauseCount;
        public int PauseCount { get => _pauseCount; set => SetProperty(ref _pauseCount, value); }

        
        private double _totalWorkTime;
        public double TotalWorkTime
        {
            get => _totalWorkTime;
            set
            {
                if (SetProperty(ref _totalWorkTime, value))
                    OnPropertyChanged(nameof(TotalWorkTimeFormatted));
            }
        }
        
        private double _totalPauseTime;
        public double TotalPauseTime
        {
            get => _totalPauseTime;
            set
            {
                if (SetProperty(ref _totalPauseTime, value))
                    OnPropertyChanged(nameof(TotalPauseTimeFormatted));
            }
        }
        
        private DateTime? _firstLogin;
        public DateTime? FirstLogin
        {
            get => _firstLogin;
            set
            {
                if (SetProperty(ref _firstLogin, value))
                    OnPropertyChanged(nameof(FirstLoginFormatted));
            }
        }
        
        private DateTime? _lastLogout;
        public DateTime? LastLogout
        {
            get => _lastLogout;
            set
            {
                if (SetProperty(ref _lastLogout, value))
                    OnPropertyChanged(nameof(LastLogoutFormatted));
            }
        }
        
        private double _totalDurationToday;
        public double TotalDurationToday
        {
            get => _totalDurationToday;
            set
            {
                if (SetProperty(ref _totalDurationToday, value))
                    OnPropertyChanged(nameof(TotalDurationTodayFormatted));
            }
        }
        
        private double _idleTime;
        public double IdleTime
        {
            get => _idleTime;
            set
            {
                if (SetProperty(ref _idleTime, value))
                    OnPropertyChanged(nameof(IdleTimeFormatted));
            }
        }
        
        private ObservableCollection<PauseDetailModel> _pauses = new ObservableCollection<PauseDetailModel>();
        public ObservableCollection<PauseDetailModel> Pauses { get => _pauses; set => SetProperty(ref _pauses, value); }
        
        public string StatusDisplay =>
            string.IsNullOrEmpty(Status) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Status.ToLower());

        public string EmployeeNameDisplay =>
            string.IsNullOrEmpty(EmployeeName) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(EmployeeName.ToLower());

        public string TotalWorkTimeFormatted => LiveTrackingFileModel.FormatMinutes(TotalWorkTime);
        public string TotalPauseTimeFormatted => LiveTrackingFileModel.FormatMinutes(TotalPauseTime);
        
        public string FirstLoginFormatted =>
            FirstLogin.HasValue ? FirstLogin.Value.ToLocalTime().ToString("hh:mm tt") : "—";
            
        public string LastLogoutFormatted =>
            LastLogout.HasValue ? LastLogout.Value.ToLocalTime().ToString("hh:mm tt") : "—";
        public string TotalDurationTodayFormatted => LiveTrackingFileModel.FormatMinutes(TotalDurationToday);
        public string IdleTimeFormatted => LiveTrackingFileModel.FormatMinutes(IdleTime);
    }

    public class IdleUserModel : ViewModelBase
    {
        private string _username = string.Empty;
        public string Username { get => _username; set => SetProperty(ref _username, value); }

        private DateTime? _firstLogin;
        public DateTime? FirstLogin
        {
            get => _firstLogin;
            set
            {
                if (SetProperty(ref _firstLogin, value))
                {
                    OnPropertyChanged(nameof(FirstLoginFormatted));
                }
            }
        }

        private double _totalDurationMinutes;
        public double TotalDurationMinutes
        {
            get => _totalDurationMinutes;
            set
            {
                if (SetProperty(ref _totalDurationMinutes, value))
                {
                    OnPropertyChanged(nameof(IdleTimeFormatted));
                }
            }
        }

        public string Status => "Idle";

        public string UsernameDisplay =>
            string.IsNullOrEmpty(Username) ? "—" : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Username.ToLower());

        public string FirstLoginFormatted =>
            FirstLogin.HasValue ? FirstLogin.Value.ToLocalTime().ToString("hh:mm tt") : "—";

        public string IdleTimeFormatted => LiveTrackingFileModel.FormatMinutes(TotalDurationMinutes);
    }
}
