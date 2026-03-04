using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.Views.ExplorerV2.Models;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace SCHLStudio.App.ViewModels.ExplorerV2
{
    public sealed class ExplorerV2ViewModel : ViewModelBase
    {
        public Action? StartActionOverride { get; set; }
        public Action? OpenWithActionOverride { get; set; }
        public Action? SkipActionOverride { get; set; }
        public Action? WalkOutActionOverride { get; set; }
        public Action? BreakActionOverride { get; set; }
        public Action? FinishActionOverride { get; set; }
        public Action? ReadyToUploadActionOverride { get; set; }

        public RelayCommand StartCommand { get; }
        public RelayCommand OpenWithCommand { get; }
        public RelayCommand SkipCommand { get; }
        public RelayCommand WalkOutCommand { get; }
        public RelayCommand BreakCommand { get; }
        public RelayCommand FinishCommand { get; }
        public RelayCommand ReadyToUploadCommand { get; }

        public ExplorerV2ViewModel()
        {
            StartCommand = new RelayCommand(_ =>
            {
                StartActionOverride?.Invoke();
            }, _ => CanExecuteStart);
            OpenWithCommand = new RelayCommand(_ =>
            {
                OpenWithActionOverride?.Invoke();
            });
            SkipCommand = new RelayCommand(_ =>
            {
                SkipActionOverride?.Invoke();
            }, _ => CanExecuteSkip);
            WalkOutCommand = new RelayCommand(_ =>
            {
                WalkOutActionOverride?.Invoke();
            }, _ => CanExecuteWalkOut);
            BreakCommand = new RelayCommand(_ =>
            {
                BreakActionOverride?.Invoke();
            }, _ => CanExecuteBreak);
            FinishCommand = new RelayCommand(_ =>
            {
                FinishActionOverride?.Invoke();
            }, _ => CanExecuteFinish);
            ReadyToUploadCommand = new RelayCommand(_ =>
            {
                ReadyToUploadActionOverride?.Invoke();
            });
        }

        public bool CanExecuteStart => !IsStarted && SelectedFilePaths.Count > 0;

        public bool CanExecuteBreak => IsStarted && !IsPaused && SelectedFilePaths.Count > 0;

        private bool _isStarted;
        public bool IsStarted
        {
            get => _isStarted;
            set
            {
                if (SetProperty(ref _isStarted, value))
                {
                    StartCommand.RaiseCanExecuteChanged();
                    BreakCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    StartCommand.RaiseCanExecuteChanged();
                    BreakCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string? _selectedBreakReason;
        public string? SelectedBreakReason
        {
            get => _selectedBreakReason;
            set => SetProperty(ref _selectedBreakReason, value);
        }

        private string? _breakNote;
        public string? BreakNote
        {
            get => _breakNote;
            set => SetProperty(ref _breakNote, value);
        }

        private string? _selectedWorkType;
        public string? SelectedWorkType
        {
            get => _selectedWorkType;
            set => SetProperty(ref _selectedWorkType, value);
        }

        private static bool IsWorkType(string? workType, string expected)
        {
            try
            {
                var wt = (workType ?? string.Empty).Trim();
                return !string.IsNullOrWhiteSpace(wt)
                       && string.Equals(wt, expected, System.StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(IsWorkType), ex);
                return false;
            }
        }

        public bool IsProductionWorkType => IsWorkType(SelectedWorkType, "Production");
        public bool IsQc1WorkType => IsWorkType(SelectedWorkType, "QC 1");
        public bool IsQc2WorkType => IsWorkType(SelectedWorkType, "QC 2");
        public bool IsQcAcWorkType => IsWorkType(SelectedWorkType, "QC AC");
        public bool IsTestFileWorkType => IsWorkType(SelectedWorkType, "Test File");
        public bool IsAdditionalWorkType => IsWorkType(SelectedWorkType, "Additional");
        public bool IsSharedWorkType => IsWorkType(SelectedWorkType, "Shared");
        public bool IsTranningWorkType => IsWorkType(SelectedWorkType, "Tranning");

        public bool IsQcWorkType => IsQc1WorkType || IsQc2WorkType;

        private string _workTypeButtonText = "Work Type";
        public string WorkTypeButtonText
        {
            get => _workTypeButtonText;
            set => SetProperty(ref _workTypeButtonText, value);
        }

        private string? _selectedOpenWith;
        public string? SelectedOpenWith
        {
            get => _selectedOpenWith;
            set => SetProperty(ref _selectedOpenWith, value);
        }

        private string _openWithButtonText = "Open With";
        public string OpenWithButtonText
        {
            get => _openWithButtonText;
            set => SetProperty(ref _openWithButtonText, value);
        }

        private string? _activeJobClientCode;
        public string? ActiveJobClientCode
        {
            get => _activeJobClientCode;
            set => SetProperty(ref _activeJobClientCode, value);
        }

        private string? _activeJobFolderPath;
        public string? ActiveJobFolderPath
        {
            get => _activeJobFolderPath;
            set => SetProperty(ref _activeJobFolderPath, value);
        }

        private string? _activeJobTaskRaw;
        public string? ActiveJobTaskRaw
        {
            get => _activeJobTaskRaw;
            set => SetProperty(ref _activeJobTaskRaw, value);
        }

        private ObservableCollection<FileTileItem> _fileTiles = new ObservableCollection<FileTileItem>();
        public ObservableCollection<FileTileItem> FileTiles
        {
            get => _fileTiles;
            private set => SetProperty(ref _fileTiles, value);
        }

        public void ReplaceFileTiles(IEnumerable<FileTileItem> items)
        {
            try
            {
                var safe = (items ?? Enumerable.Empty<FileTileItem>())
                    .Where(x => x is not null)
                    .ToList();

                FileTiles = new ObservableCollection<FileTileItem>(safe);
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(ReplaceFileTiles), ex);
            }
        }

        public ObservableCollection<SelectedFileRow> SelectedFiles { get; } = new ObservableCollection<SelectedFileRow>();

        public ObservableCollection<string> WorkTypes { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> PauseReasons { get; } = new ObservableCollection<string>();

        public HashSet<string> SelectedFilePaths { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        private readonly List<SelectedFileRow> _highlightedFiles = new List<SelectedFileRow>();

        public IReadOnlyList<SelectedFileRow> HighlightedFiles => _highlightedFiles;

        public void UpdateHighlightedFiles(List<SelectedFileRow> files)
        {
            _highlightedFiles.Clear();
            if (files != null)
            {
                _highlightedFiles.AddRange(files);
            }
            WalkOutCommand.RaiseCanExecuteChanged();
            SkipCommand.RaiseCanExecuteChanged();
        }

        public bool CanExecuteFinish => IsStarted && !IsPaused && SelectedFilePaths.Count > 0;
        public bool CanExecuteWalkOut => IsStarted && !IsPaused && _highlightedFiles.Count > 0;
        public bool CanExecuteSkip => !IsPaused && _highlightedFiles.Count > 0;

        private string _timerDisplay = "00:00:00";
        public string TimerDisplay
        {
            get => _timerDisplay;
            set => SetProperty(ref _timerDisplay, value);
        }

        private bool _hasSelectionMetaLock;
        public bool HasSelectionMetaLock
        {
            get => _hasSelectionMetaLock;
            private set => SetProperty(ref _hasSelectionMetaLock, value);
        }

        private string? _selectionLockedClientCode;
        public string? SelectionLockedClientCode
        {
            get => _selectionLockedClientCode;
            private set => SetProperty(ref _selectionLockedClientCode, value);
        }

        private string? _selectionLockedWorkType;
        public string? SelectionLockedWorkType
        {
            get => _selectionLockedWorkType;
            private set => SetProperty(ref _selectionLockedWorkType, value);
        }

        private List<string> _selectionLockedTasks = new List<string>();
        public IReadOnlyList<string> SelectionLockedTasks => _selectionLockedTasks;

        private int? _selectionLockedEt;
        public int? SelectionLockedEt
        {
            get => _selectionLockedEt;
            private set => SetProperty(ref _selectionLockedEt, value);
        }

        private string _selectedFilesMetaText = string.Empty;
        public string SelectedFilesMetaText
        {
            get => _selectedFilesMetaText;
            set => SetProperty(ref _selectedFilesMetaText, value);
        }

        private string _startButtonText = "Start";
        public string StartButtonText
        {
            get => _startButtonText;
            set => SetProperty(ref _startButtonText, value);
        }

        private bool _isFinishEnabled;
        public bool IsFinishEnabled
        {
            get => _isFinishEnabled;
            set => SetProperty(ref _isFinishEnabled, value);
        }

        private bool _isWalkOutEnabled;
        public bool IsWalkOutEnabled
        {
            get => _isWalkOutEnabled;
            set => SetProperty(ref _isWalkOutEnabled, value);
        }

        private bool _isSkipEnabled;
        public bool IsSkipEnabled
        {
            get => _isSkipEnabled;
            set => SetProperty(ref _isSkipEnabled, value);
        }

        public void ResetActionState()
        {
            try
            {
                IsFinishEnabled = false;
                IsWalkOutEnabled = false;
                IsSkipEnabled = false;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(ResetActionState), ex);
            }
        }

        public void EnableActions()
        {
            try
            {
                IsFinishEnabled = true;
                IsWalkOutEnabled = true;
                IsSkipEnabled = true;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(EnableActions), ex);
            }
        }

        public void ClearSelection()
        {
            try
            {
                try
                {
                    SelectedFiles.Clear();
                }
                catch (Exception ex)
                {
                    LogNonCritical("ClearSelection.SelectedFiles", ex);
                }

                try
                {
                    SelectedFilePaths?.Clear();
                }
                catch (Exception ex)
                {
                    LogNonCritical("ClearSelection.SelectedFilePaths", ex);
                }

                ClearSelectionMetaLock();

                StartCommand.RaiseCanExecuteChanged();
                BreakCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(ClearSelection), ex);
            }
        }

        public void RemoveFile(string? fullPath)
        {
            try
            {
                var fp = (fullPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(fp))
                {
                    return;
                }

                try
                {
                    SelectedFilePaths?.Remove(fp);
                }
                catch (Exception ex)
                {
                    LogNonCritical("RemoveFile.SelectedFilePaths", ex);
                }

                try
                {
                    for (var i = SelectedFiles.Count - 1; i >= 0; i--)
                    {
                        var p = (SelectedFiles[i]?.FullPath ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(p) && string.Equals(p, fp, System.StringComparison.OrdinalIgnoreCase))
                        {
                            SelectedFiles.RemoveAt(i);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogNonCritical("RemoveFile.SelectedFiles", ex);
                }

                if ((SelectedFilePaths?.Count ?? 0) == 0)
                {
                    ClearSelectionMetaLock();
                }

                StartCommand.RaiseCanExecuteChanged();
                BreakCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(RemoveFile), ex);
            }
        }

        public void ClearSelectionMetaLock()
        {
            try
            {
                HasSelectionMetaLock = false;
                SelectionLockedClientCode = null;
                SelectionLockedWorkType = null;
                _selectionLockedTasks = new List<string>();
                SelectionLockedEt = null;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(ClearSelectionMetaLock), ex);
            }
        }

        public void LockSelectionMeta(string? clientCode, string? workType, IReadOnlyList<string>? tasks, int? et)
        {
            try
            {
                HasSelectionMetaLock = true;
                SelectionLockedClientCode = (clientCode ?? string.Empty).Trim();
                SelectionLockedWorkType = (workType ?? string.Empty).Trim();
                _selectionLockedTasks = (tasks ?? Array.Empty<string>())
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                SelectionLockedEt = et;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(LockSelectionMeta), ex);
            }
        }


        public void RefreshMetaText(bool showClient, string? clientCode, string? workType, IReadOnlyList<string>? tasks, int? et)
        {
            try
            {
                var cc = (clientCode ?? string.Empty).Trim();
                var wt = (workType ?? string.Empty).Trim();
                var t = tasks?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();

                var etText = string.Empty;
                try
                {
                    if (showClient && !string.IsNullOrWhiteSpace(cc) && et is not null)
                    {
                        etText = "ET: " + et.Value;
                    }
                }
                catch (Exception ex)
                {
                    LogNonCritical("RefreshMetaText.ET", ex);
                    etText = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(wt) && t.Count == 0)
                {
                    SelectedFilesMetaText = !showClient || string.IsNullOrWhiteSpace(cc)
                        ? string.Empty
                        : (string.IsNullOrWhiteSpace(etText)
                            ? ("Client: " + cc)
                            : ("Client: " + cc + "   |   " + etText));
                    return;
                }

                if (t.Count == 0)
                {
                    SelectedFilesMetaText = !showClient || string.IsNullOrWhiteSpace(cc)
                        ? ("Work Type: " + wt)
                        : (string.IsNullOrWhiteSpace(etText)
                            ? ("Client: " + cc + "   |   Work Type: " + wt)
                            : ("Client: " + cc + "   |   " + etText + "   |   Work Type: " + wt));
                    return;
                }

                SelectedFilesMetaText = !showClient || string.IsNullOrWhiteSpace(cc)
                    ? ("Work Type: " + wt + "   |   Task: " + string.Join(", ", t))
                    : (string.IsNullOrWhiteSpace(etText)
                        ? ("Client: " + cc + "   |   Work Type: " + wt + "   |   Task: " + string.Join(", ", t))
                        : ("Client: " + cc + "   |   " + etText + "   |   Work Type: " + wt + "   |   Task: " + string.Join(", ", t)));
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(RefreshMetaText), ex);
            }
        }

        private static void LogNonCritical(string operation, Exception ex)
        {
            try
            {
                Debug.WriteLine($"[ExplorerV2ViewModel] {operation} non-critical: {ex.Message}");
            }
            catch
            {
            }
        }

        private Visibility _busyOverlayVisibility = Visibility.Collapsed;
        public Visibility BusyOverlayVisibility
        {
            get => _busyOverlayVisibility;
            set => SetProperty(ref _busyOverlayVisibility, value);
        }

        private string _busyMessage = "Processing...";
        public string BusyMessage
        {
            get => _busyMessage;
            set => SetProperty(ref _busyMessage, value);
        }
    }
}
