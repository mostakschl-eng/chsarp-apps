using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using SCHLStudio.App.ViewModels.ExplorerV2;
using SCHLStudio.App.Views.ExplorerV2.Models;
using SCHLStudio.App.Views.ExplorerV2.Services;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View : System.Windows.Controls.UserControl
    {
        private readonly ExplorerV2ViewModel _vm = new ExplorerV2ViewModel();

        private bool _isFinishRunning;
        private bool _isWalkOutRunning;
        private bool _isSkipRunning;
        private bool _isReadyToUploadRunning;

        private static readonly string[] DefaultWorkTypes =
        {
            "Production",
            "QC 1",
            "QC 2",
            "QC AC",
            "Test File",
            "Additional",
            "Shared",
            "Tranning"
        };

        private IReadOnlyList<string> _workTypes = DefaultWorkTypes;
        private bool _roleRestrictionsApplied;

        private readonly string[] _openWithOptions =
        {
            "Photoshop 26",
            "Photoshop 25",
            "Photoshop CC"
        };

        private readonly DoneMoveService _doneMoveService = new DoneMoveService();
        private readonly IExplorerV2WorkflowService _workflowService;

        private DispatcherTimer? _trackerInitTimer;

        private bool _isRubberbandSelecting;
        private System.Windows.Point _rubberbandStart;
        private RubberbandAdorner? _rubberbandAdorner;
        private AdornerLayer? _rubberbandLayer;
        private DateTime _lastRubberbandSelectionUtc = DateTime.MinValue;
        private Rect _lastRubberbandRect = Rect.Empty;

        private System.Windows.Point _filesDragStart;
        private bool _isFilesDragArmed;

        private readonly List<JobListRow> _jobListRows = new();
        private HashSet<string> _activeJobTasks = new(StringComparer.OrdinalIgnoreCase);

        // ── Cached brushes (resolved once on Loaded, avoids TryFindResource per tick) ──
        private System.Windows.Media.Brush? _cachedDividerBrush;
        private System.Windows.Media.Brush? _cachedPrimaryBrush;
        private System.Windows.Media.Brush? _cachedDangerBrush;
        private System.Windows.Media.Brush? _cachedWarningBrush;
        private System.Windows.Media.Brush? _cachedTextMainBrush;
        private System.Windows.Media.Brush? _cachedTextWhiteBrush;
        private System.Windows.Media.Brush? _cachedTextBlackBrush;

        // ── Cached active job ET (updated on job selection, avoids LINQ scan every tick) ──
        private int _cachedActiveJobEtMinutes;

        private static void RunNonCritical(Action action, string operation = "ExplorerV2.NonCriticalAction")
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                NonCriticalLog.IncrementAndLog("ExplorerV2", operation, ex);
            }
        }

        private static void RunNonCriticalAsync(Func<Task> action, string operation = "ExplorerV2.NonCriticalAsync")
        {
            try
            {
                var task = action();
                task.ContinueWith(
                    t =>
                    {
                        var ex = t.Exception?.GetBaseException() ?? t.Exception;
                        if (ex is not null)
                        {
                            NonCriticalLog.IncrementAndLog("ExplorerV2", operation, ex);
                        }
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                NonCriticalLog.IncrementAndLog("ExplorerV2", operation, ex);
            }
        }

        private static T GetNonCritical<T>(Func<T> func, T fallback, string operation = "ExplorerV2.NonCriticalGet")
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                NonCriticalLog.IncrementAndLog("ExplorerV2", operation, ex);
                return fallback;
            }
        }

        private static void LogSuppressedError(string operation, Exception ex)
        {
            try
            {
                NonCriticalLog.EnqueueError("ExplorerV2", operation, ex);
            }
            catch
            {
            }
        }

        public ExplorerV2View()
        {
            InitializeComponent();

            var app = System.Windows.Application.Current as SCHLStudio.App.App;
            _workflowService = (app?.ServiceProvider?.GetService(typeof(IExplorerV2WorkflowService)) as IExplorerV2WorkflowService)
                ?? new ExplorerV2WorkflowService();

            _workTypes = LoadExplorerV2WorkTypes();

            RunNonCritical(() => Loaded += ExplorerV2View_Loaded, "ExplorerV2.Ctor.AttachLoaded");
            RunNonCritical(() => Unloaded += ExplorerV2View_Unloaded, "ExplorerV2.Ctor.AttachUnloaded");

            RunNonCritical(() => DataContext = _vm, "ExplorerV2.Ctor.SetDataContext");

            _vm.StartActionOverride = ExecuteStartWorkflowFromVm;
            _vm.OpenWithActionOverride = ExecuteOpenWithWorkflowFromVm;
            _vm.SkipActionOverride = ExecuteSkipWorkflowFromVm;
            _vm.WalkOutActionOverride = ExecuteWalkOutWorkflowFromVm;
            _vm.BreakActionOverride = ExecuteBreakWorkflowFromVm;
            _vm.FinishActionOverride = ExecuteFinishWorkflowFromVm;
            _vm.ReadyToUploadActionOverride = ExecuteReadyToUploadWorkflowFromVm;

            RunNonCritical(BuildWorkTypeMenu, "ExplorerV2.Ctor.BuildWorkTypeMenu");
            RunNonCritical(BuildTaskMenu, "ExplorerV2.Ctor.BuildTaskMenu");
            RunNonCritical(BuildOpenWithMenu, "ExplorerV2.Ctor.BuildOpenWithMenu");
            RunNonCritical(ResetActionButtons, "ExplorerV2.Ctor.ResetActionButtons");
            RunNonCritical(UpdateSelectedFilesMetaText, "ExplorerV2.Ctor.UpdateSelectedFilesMetaText");
        }

        private void ExplorerV2View_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_roleRestrictionsApplied)
            {
                _roleRestrictionsApplied = true;
                RunNonCritical(ApplyRoleBasedUiRestrictions, "ExplorerV2.Loaded.ApplyRoleBasedUiRestrictions");
            }

            RunNonCritical(CacheBrushes, "ExplorerV2.Loaded.CacheBrushes");

            try
            {
                if (_vm.IsStarted)
                {
                    RunNonCritical(StartIdleMonitor, "ExplorerV2.Loaded.StartIdleMonitor");
                }
            }
            catch
            {
            }

            RunNonCritical(StartTrackerSyncWhenUserAvailable, "ExplorerV2.Loaded.StartTrackerSyncWhenUserAvailable");

            // Auto-open the Job List panel on app load
            RunNonCritical(OpenJobListPanel, "ExplorerV2.Loaded.OpenJobListPanel");
            RunNonCriticalAsync(() => LoadJobListFromApiAsync(), "ExplorerV2.Loaded.LoadJobList");
        }

        private void CacheBrushes()
        {
            _cachedDividerBrush = TryFindResource("DividerBrush") as System.Windows.Media.Brush;
            _cachedPrimaryBrush = TryFindResource("PrimaryBrush") as System.Windows.Media.Brush;
            _cachedDangerBrush = TryFindResource("DangerBrush") as System.Windows.Media.Brush;
            _cachedWarningBrush   = TryFindResource("WarningBrush")   as System.Windows.Media.Brush;
            _cachedTextMainBrush  = TryFindResource("TextMainBrush")  as System.Windows.Media.Brush;
            _cachedTextWhiteBrush = TryFindResource("TextWhiteBrush") as System.Windows.Media.Brush;
            _cachedTextBlackBrush = TryFindResource("TextBlackBrush") as System.Windows.Media.Brush;
        }

        private void StartTrackerSyncWhenUserAvailable()
        {
            try
            {
                if (_trackerSync is not null)
                {
                    return;
                }

                // Immediately grab the username from RAM
                var user = GetAppCurrentUser();
                if (!string.IsNullOrWhiteSpace(user))
                {
                    InitializeTrackerSync(user);
                }
                else
                {
                    // Fallback: If it's somehow completely empty on the very first render frame,
                    // we will check exactly once more after a tiny delay, then force start it.
                    _trackerInitTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    _trackerInitTimer.Tick += (_, __) =>
                    {
                        try
                        {
                            var delayedUser = GetAppCurrentUser();
                            if (!string.IsNullOrWhiteSpace(delayedUser))
                            {
                                InitializeTrackerSync(delayedUser);
                            }
                        }
                        finally
                        {
                            // Always shut down the timer immediately after the first fallback attempt
                            try { _trackerInitTimer?.Stop(); } catch { }
                        }
                    };
                    _trackerInitTimer.Start();
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
            }
        }

        private void ExplorerV2View_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_vm.IsStarted)
            {
                RunNonCritical(StopIdleMonitor, "ExplorerV2.Unloaded.StopIdleMonitor");
            }

            try
            {
                _trackerInitTimer?.Stop();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
            }

            RunNonCritical(() => _fileIndexCts?.Cancel(), "ExplorerV2.Unloaded.FileIndexCts.Cancel");
            RunNonCritical(() => _fileIndexCts?.Dispose(), "ExplorerV2.Unloaded.FileIndexCts.Dispose");
            _fileIndexCts = null;

            RunNonCritical(() => _jobListLoadCts?.Cancel(), "ExplorerV2.Unloaded.JobListLoadCts.Cancel");
            RunNonCritical(() => _jobListLoadCts?.Dispose(), "ExplorerV2.Unloaded.JobListLoadCts.Dispose");
            _jobListLoadCts = null;

            RunNonCritical(() => _filesRefreshDebounceTimer?.Stop(), "ExplorerV2.Unloaded.FilesRefreshDebounce.Stop");
            _filesRefreshDebounceTimer = null;
            _pendingFilesRefreshPath = null;
        }

        private string GetAppCurrentUser()
        {
            return GetNonCritical(() => FileOperationHelper.GetAppCurrentUser(this), string.Empty, "ExplorerV2.GetAppCurrentUser");
        }

        private string GetAppCurrentRole()
        {
            return GetNonCritical(() => FileOperationHelper.GetAppCurrentRole(this), string.Empty, "ExplorerV2.GetAppCurrentRole");
        }

        private bool IsEmployeeRole()
        {
            var role = GetAppCurrentRole();
            return string.Equals(role, "employee", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedEmployeeWorkType(string? workType)
        {
            var normalized = (workType ?? string.Empty).Trim();
            return string.Equals(normalized, "Production", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Additional", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Shared", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Tranning", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> FilterWorkTypesForRole(IReadOnlyList<string> source, bool isEmployee)
        {
            if (!isEmployee)
            {
                return source;
            }

            var filtered = source
                .Where(IsAllowedEmployeeWorkType)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!filtered.Any(x => string.Equals(x, "Production", StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Insert(0, "Production");
            }

            if (!filtered.Any(x => string.Equals(x, "Additional", StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add("Additional");
            }

            if (!filtered.Any(x => string.Equals(x, "Shared", StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add("Shared");
            }

            if (!filtered.Any(x => string.Equals(x, "Tranning", StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add("Tranning");
            }

            filtered = filtered
                .OrderBy(x => string.Equals(x, "Production", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();

            return filtered;
        }

        private void ApplyRoleBasedUiRestrictions()
        {
            var isEmployee = IsEmployeeRole();

            _workTypes = FilterWorkTypesForRole(LoadExplorerV2WorkTypes(), isEmployee);
            BuildWorkTypeMenu();

            if (isEmployee && !IsAllowedEmployeeWorkType(_vm.WorkTypeButtonText))
            {
                _vm.WorkTypeButtonText = "Work Type";
                _vm.SelectedWorkType = null;
            }

            UpdateSelectedFilesMetaText();
            ApplyFileFilterRoleRestrictions(isEmployee);
        }

        private void ApplyFileFilterRoleRestrictions(bool isEmployee)
        {
            if (FilesProductionDoneButton is not null)
            {
                FilesProductionDoneButton.IsEnabled = !isEmployee;
                FilesProductionDoneButton.Visibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;
                if (isEmployee)
                {
                    FilesProductionDoneButton.IsChecked = false;
                }
            }

            if (FilesQc1DoneButton is not null)
            {
                FilesQc1DoneButton.IsEnabled = !isEmployee;
                FilesQc1DoneButton.Visibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;
                if (isEmployee)
                {
                    FilesQc1DoneButton.IsChecked = false;
                }
            }

            if (FilesQc2DoneButton is not null)
            {
                var role = GetAppCurrentRole();
                var hideQc2Done = isEmployee ||
                                  string.Equals(role, "qcmanager", StringComparison.OrdinalIgnoreCase);

                FilesQc2DoneButton.IsEnabled = !hideQc2Done;
                FilesQc2DoneButton.Visibility = hideQc2Done ? Visibility.Collapsed : Visibility.Visible;
                if (hideQc2Done)
                {
                    FilesQc2DoneButton.IsChecked = false;
                }
            }

            if (ReadyToUploadButton is not null)
            {
                ReadyToUploadButton.IsEnabled = !isEmployee;
                ReadyToUploadButton.Visibility = isEmployee ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void PostActionCleanup(string? baseDir)
        {
            RunNonCritical(UpdateSelectedFilesMetaText, "ExplorerV2.PostActionCleanup.UpdateSelectedFilesMetaText");
            RunNonCritical(() => RefreshFileTilesForCurrentContext(baseDir), "ExplorerV2.PostActionCleanup.RefreshFileTiles");
        }

        private readonly struct WorkTypeInfo
        {
            public WorkTypeInfo(string? workType)
            {
                Name = (workType ?? string.Empty).Trim();

                var t = Name;
                IsProduction = string.Equals(t, "Production", StringComparison.OrdinalIgnoreCase);
                IsQc1 = string.Equals(t, "QC 1", StringComparison.OrdinalIgnoreCase);
                IsQc2 = string.Equals(t, "QC 2", StringComparison.OrdinalIgnoreCase);
                IsQcAc = string.Equals(t, "QC AC", StringComparison.OrdinalIgnoreCase);
                IsTestFile = string.Equals(t, "Test File", StringComparison.OrdinalIgnoreCase);
                IsAdditional = string.Equals(t, "Additional", StringComparison.OrdinalIgnoreCase);
                IsShared = string.Equals(t, "Shared", StringComparison.OrdinalIgnoreCase);
                IsTranning = string.Equals(t, "Tranning", StringComparison.OrdinalIgnoreCase);
            }

            public string Name { get; }
            public bool IsProduction { get; }
            public bool IsQc1 { get; }
            public bool IsQc2 { get; }
            public bool IsQc => IsQc1 || IsQc2;
            public bool IsQcAc { get; }
            public bool IsTestFile { get; }
            public bool IsAdditional { get; }
            public bool IsShared { get; }
            public bool IsTranning { get; }
        }

        private WorkTypeInfo GetCurrentWorkTypeInfo()
        {
            var workType = GetNonCritical(() => (WorkTypeButton?.Content as string) ?? string.Empty, string.Empty, "ExplorerV2.GetCurrentWorkTypeInfo");
            return new WorkTypeInfo(workType);
        }

        private string GetActiveJobFolderPath()
        {
            return GetNonCritical(() =>
            {
                if (!string.IsNullOrWhiteSpace(_vm.ActiveJobFolderPath))
                {
                    return _vm.ActiveJobFolderPath;
                }

                return _jobListRows
                    .FirstOrDefault(x => string.Equals(x.ClientCode, _vm.ActiveJobClientCode, StringComparison.OrdinalIgnoreCase))
                    ?.FolderPath ?? string.Empty;
            }, string.Empty, "ExplorerV2.GetActiveJobFolderPath");
        }

        private void RemoveFileFromSelection(string? fullPath)
        {
            var fp = (fullPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fp))
            {
                return;
            }

            RunNonCritical(() => _vm.RemoveFile(fp), "ExplorerV2.RemoveFileFromSelection");
        }

        private static IReadOnlyList<string> LoadExplorerV2WorkTypes()
        {
            try
            {
                var cfg = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    as Microsoft.Extensions.Configuration.IConfiguration;

                var list = new List<string>();
                try
                {
                    foreach (var child in (cfg?.GetSection("WorkTypes")?.GetSection("ExplorerV2")?.GetChildren()
                                 ?? Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>()))
                    {
                        var v = (child?.Value ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            list.Add(v);
                        }
                    }
                }
                catch
                {
                    list.Clear();
                }

                list = list
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (list.Count == 0)
                {
                    return DefaultWorkTypes;
                }

                // Ensure Production is always present and first
                if (!list.Any(x => string.Equals(x, "Production", StringComparison.OrdinalIgnoreCase)))
                {
                    list.Insert(0, "Production");
                }
                else
                {
                    var prodIdx = list.FindIndex(x => string.Equals(x, "Production", StringComparison.OrdinalIgnoreCase));
                    if (prodIdx > 0)
                    {
                        var prod = list[prodIdx];
                        list.RemoveAt(prodIdx);
                        list.Insert(0, prod);
                    }
                }

                if (!list.Any(x => string.Equals(x, "Tranning", StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add("Tranning");
                }

                return list;
            }
            catch
            {
                return DefaultWorkTypes;
            }
        }

        private static string ToShortFileName(string fileName)
        {
            try
            {
                var name = (fileName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return string.Empty;
                }

                if (name.Length <= 44)
                {
                    return name;
                }

                var first = name.Substring(0, 18);
                var last = name.Substring(name.Length - 18, 18);
                return first + "..." + last;
            }
            catch
            {
                return fileName ?? string.Empty;
            }
        }

        private void UpdateSelectedFilesMetaText()
        {
            try
            {
                var showClient = false;
                try
                {
                    showClient = _vm.SelectedFiles.Count > 0;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
                }

                var workType = (WorkTypeButton?.Content as string) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(workType) || string.Equals(workType, "Work Type", StringComparison.OrdinalIgnoreCase))
                {
                    workType = string.Empty;
                }

                var tasks = new List<string>();
                try
                {
                    tasks = TaskMenu?.Items.OfType<MenuItem>()
                        .Where(x => x.IsChecked)
                        .Select(x => (x.Header as string) ?? string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList() ?? new List<string>();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
                }

                var etText = string.Empty;
                int? etValue = null;
                try
                {
                    if (showClient && !string.IsNullOrWhiteSpace(_vm.ActiveJobClientCode))
                    {
                        var et = _jobListRows
                            .FirstOrDefault(x =>
                                string.Equals(x.ClientCode, _vm.ActiveJobClientCode, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(x.FolderPath, _vm.ActiveJobFolderPath, StringComparison.OrdinalIgnoreCase))
                            ?.ET;

                        if (et is not null)
                        {
                            etText = "ET: " + et.Value;
                            etValue = et.Value;
                        }
                    }
                }
                catch
                {
                    etText = string.Empty;
                    etValue = null;
                }

                try
                {
                    if (showClient && _vm.IsStarted && !_vm.HasSelectionMetaLock)
                    {
                        _vm.LockSelectionMeta(_vm.ActiveJobClientCode, workType, tasks, etValue);
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
                }

                var clientCodeForMeta = _vm.ActiveJobClientCode;

                if (showClient && _vm.HasSelectionMetaLock)
                {
                    try
                    {
                        clientCodeForMeta = _vm.SelectionLockedClientCode ?? string.Empty;
                        workType = _vm.SelectionLockedWorkType ?? string.Empty;
                        tasks = _vm.SelectionLockedTasks.ToList();
                        etValue = _vm.SelectionLockedEt;
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
                    }
                }

                try
                {
                    _vm.RefreshMetaText(showClient, clientCodeForMeta, workType, tasks, etValue);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.xaml", ex_safe_log);
            }
        }

        private void SelectedFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var selectedItems = SelectedFilesListBox.SelectedItems.OfType<SelectedFileRow>().ToList();
                _vm.UpdateHighlightedFiles(selectedItems);
            }
            catch (Exception ex)
            {
                LogSuppressedError("SelectedFilesListBox_SelectionChanged", ex);
            }
        }

        private void SelectedFileCopyPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe)
                {
                    return;
                }

                var text = (fe.Tag as string ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = Path.GetDirectoryName(text)?.Trim() ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    System.Windows.Clipboard.SetText(text);
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogSuppressedError("SelectedFileCopyPathButton_Click", ex);
            }
        }

    }
}
