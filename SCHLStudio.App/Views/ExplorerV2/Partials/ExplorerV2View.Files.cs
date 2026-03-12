using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.Views.ExplorerV2.Models;
using SCHLStudio.App.Views.ExplorerV2.Services;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private readonly FileIndexService _fileIndexService = new FileIndexService();
        private CancellationTokenSource? _fileIndexCts;

        private readonly object _fileScanMetricsLock = new object();
        private readonly Queue<long> _fileScanDurationsMs = new Queue<long>(capacity: 64);
        private readonly List<long> _fileScanDurationsSortedMs = new List<long>(capacity: 64);
        private int _fileScanCount;

        private DispatcherTimer? _filesRefreshDebounceTimer;
        private string? _pendingFilesRefreshPath;

        private string _filesContextPath = string.Empty;

        private readonly FileIndexService.FilesViewMode _productionDoneMode = FileIndexService.FilesViewMode.ProductionDone;
        private readonly FileIndexService.FilesViewMode _qc1DoneMode = FileIndexService.FilesViewMode.Qc1AllDone;
        private readonly FileIndexService.FilesViewMode _qc2DoneMode = FileIndexService.FilesViewMode.Qc2AllDone;

        private void RefreshFileTilesForCurrentContext(string? activeJobFolderPath)
        {
            try
            {
                var baseDir = (activeJobFolderPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    RunNonCriticalAsync(() => RefreshFileTilesForCurrentContextCore(activeJobFolderPath));
                    return;
                }

                _pendingFilesRefreshPath = activeJobFolderPath;

                if (_filesRefreshDebounceTimer is null)
                {
                    _filesRefreshDebounceTimer = new DispatcherTimer();
                    _filesRefreshDebounceTimer.Interval = TimeSpan.FromMilliseconds(200);
                    _filesRefreshDebounceTimer.Tick += (_, __) =>
                    {
                        try
                        {
                            _filesRefreshDebounceTimer?.Stop();
                        }
                        catch (Exception debEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"_filesRefreshDebounceTimer stop error: {debEx.Message}");
                        }

                        var pending = _pendingFilesRefreshPath;
                        _pendingFilesRefreshPath = null;
                        RunNonCriticalAsync(() => RefreshFileTilesForCurrentContextCore(pending));
                    };
                }

                try
                {
                    _filesRefreshDebounceTimer.Stop();
                    _filesRefreshDebounceTimer.Start();
                }
                catch (Exception ex)
                {
                    LogSuppressedError("RefreshFileTilesForCurrentContext.DebounceTimer", ex);
                    RunNonCriticalAsync(() => RefreshFileTilesForCurrentContextCore(activeJobFolderPath));
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "RefreshFileTilesForCurrentContext",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["activeJobFolderPath"] = activeJobFolderPath
                        });
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"RefreshFileTilesForCurrentContext logging error: {logEx.Message}");
                }
            }
        }

        private void TryLogFileScanMetrics(long elapsedMs, string baseDir, FileIndexService.FilesViewMode mode, int tileCount)
        {
            try
            {
                long median;
                long p95;
                int n;
                int scanCount;

                lock (_fileScanMetricsLock)
                {
                    _fileScanCount++;
                    scanCount = _fileScanCount;

                    _fileScanDurationsMs.Enqueue(elapsedMs);

                    var insertIndex = _fileScanDurationsSortedMs.BinarySearch(elapsedMs);
                    if (insertIndex < 0)
                    {
                        insertIndex = ~insertIndex;
                    }
                    _fileScanDurationsSortedMs.Insert(insertIndex, elapsedMs);

                    if (_fileScanDurationsMs.Count > 100)
                    {
                        var oldest = _fileScanDurationsMs.Dequeue();
                        var removeIndex = _fileScanDurationsSortedMs.BinarySearch(oldest);
                        if (removeIndex < 0)
                        {
                            removeIndex = ~removeIndex;
                            if (removeIndex >= 0 && removeIndex < _fileScanDurationsSortedMs.Count)
                            {
                                if (_fileScanDurationsSortedMs[removeIndex] == oldest)
                                {
                                    _fileScanDurationsSortedMs.RemoveAt(removeIndex);
                                }
                            }
                        }
                        else
                        {
                            while (removeIndex > 0 && _fileScanDurationsSortedMs[removeIndex - 1] == oldest)
                            {
                                removeIndex--;
                            }
                            _fileScanDurationsSortedMs.RemoveAt(removeIndex);
                        }
                    }

                    n = _fileScanDurationsSortedMs.Count;
                    median = n == 0 ? 0 : _fileScanDurationsSortedMs[n / 2];
                    var p95Index = n == 0 ? 0 : Math.Min(n - 1, (int)Math.Ceiling(n * 0.95) - 1);
                    p95 = n == 0 ? 0 : _fileScanDurationsSortedMs[p95Index];
                }

                AppDataLog.LogEvent(
                    area: "ExplorerV2",
                    operation: "FileScan.Metrics",
                    level: "info",
                    data: new Dictionary<string, string?>
                    {
                        ["elapsedMs"] = elapsedMs.ToString(),
                        ["tileCount"] = tileCount.ToString(),
                        ["mode"] = mode.ToString(),
                        ["baseDir"] = baseDir,
                        ["windowCount"] = n.ToString(),
                        ["medianMs"] = median.ToString(),
                        ["p95Ms"] = p95.ToString(),
                        ["scanCount"] = scanCount.ToString()
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryLogFileScanMetrics inner error: {ex.Message}");
            }
        }

        private async Task RefreshFileTilesForCurrentContextCore(string? activeJobFolderPath)
        {
            try
            {
                var baseDir = (activeJobFolderPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    _filesContextPath = string.Empty;
                    try
                    {
                        _vm.ReplaceFileTiles(Array.Empty<FileTileItem>());
                    }
                    catch (Exception repEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"ReplaceFileTiles error (empty dir): {repEx.Message}");
                    }
                    return;
                }

                _filesContextPath = baseDir;

                FileIndexService.FilesViewMode mode = FileIndexService.FilesViewMode.Work;
                try
                {
                    if (FilesProductionDoneButton?.IsChecked == true)
                    {
                        mode = _productionDoneMode;
                    }
                    else if (FilesQc1DoneButton?.IsChecked == true)
                    {
                        mode = _qc1DoneMode;
                    }
                    else if (FilesQc2DoneButton?.IsChecked == true)
                    {
                        mode = _qc2DoneMode;
                    }
                    else
                    {
                        mode = FileIndexService.FilesViewMode.Work;
                    }
                }
                catch
                {
                    mode = FileIndexService.FilesViewMode.Work;
                }

                var currentUser = GetAppCurrentUser();

                RunNonCritical(() => _fileIndexCts?.Cancel());
                RunNonCritical(() => _fileIndexCts?.Dispose());

                var cts = new CancellationTokenSource();
                _fileIndexCts = cts;
                var token = cts.Token;

                IReadOnlyList<FileTileItem> tiles = Array.Empty<FileTileItem>();
                var sw = Stopwatch.StartNew();
                try
                {
                    tiles = await Task.Run(() =>
                    {
                        var raw = _fileIndexService.BuildTiles(baseDir, mode, currentUser, token);

                        try
                        {
                            return (IReadOnlyList<FileTileItem>)raw
                                .Where(x => x is not null)
                                .OrderBy(x => (x.FolderName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                                .ThenBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase)
                                .ToList();
                        }
                        catch
                        {
                            return raw;
                        }
                    }, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    tiles = Array.Empty<FileTileItem>();
                }
                finally
                {
                    sw.Stop();
                    TryLogFileScanMetrics(sw.ElapsedMilliseconds, baseDir, mode, tiles?.Count ?? 0);
                }

                if (!ReferenceEquals(cts, _fileIndexCts))
                {
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    _vm.ReplaceFileTiles(tiles ?? Array.Empty<FileTileItem>());
                }
                catch (Exception addEx)
                {
                    System.Diagnostics.Debug.WriteLine($"RefreshFileTilesForCurrentContextCore adding tiles error: {addEx.Message}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "RefreshFileTilesForCurrentContextCore",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["activeJobFolderPath"] = activeJobFolderPath,
                            ["filesContextPath"] = _filesContextPath
                        });
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"RefreshFileTilesForCurrentContextCore logging error: {logEx.Message}");
                }
            }
        }

        private void FileFilterToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Primitives.ToggleButton tb)
                {
                    if (IsEmployeeRole()
                        && (ReferenceEquals(tb, FilesProductionDoneButton)
                            || ReferenceEquals(tb, FilesQc1DoneButton)
                            || ReferenceEquals(tb, FilesQc2DoneButton)))
                    {
                        tb.IsChecked = false;
                        if (FilesWorkButton is not null)
                        {
                            FilesWorkButton.IsChecked = true;
                            tb = FilesWorkButton;
                        }
                    }

                    if (!ReferenceEquals(tb, FilesWorkButton)) FilesWorkButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                    if (!ReferenceEquals(tb, FilesProductionDoneButton)) FilesProductionDoneButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                    if (!ReferenceEquals(tb, FilesQc1DoneButton)) FilesQc1DoneButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                    if (!ReferenceEquals(tb, FilesQc2DoneButton)) FilesQc2DoneButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);

                    tb.IsChecked = true;
                }

                try
                {
                    var baseDir = string.Empty;

                    baseDir = GetActiveJobFolderPath();

                    RefreshFileTilesForCurrentContext(baseDir);
                }
                catch (Exception refEx)
                {
                    System.Diagnostics.Debug.WriteLine($"FileFilterToggle_Checked refresh error: {refEx.Message}");
                }
            }
            catch (Exception togEx)
            {
                System.Diagnostics.Debug.WriteLine($"FileFilterToggle_Checked error: {togEx.Message}");
            }
        }

        private void ReloadFilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var baseDir = string.Empty;
                baseDir = GetActiveJobFolderPath();

                _fileIndexService.InvalidateDoneRootCache(baseDir);

                RefreshFileTilesForCurrentContext(baseDir);
            }
            catch (Exception relEx)
            {
                System.Diagnostics.Debug.WriteLine($"ReloadFilesButton_Click error: {relEx.Message}");
            }
        }

        private void ExecuteReadyToUploadWorkflowFromVm()
        {
            _ = SafeExecuteReadyToUploadWorkflowAsync();
        }

        private async Task SafeExecuteReadyToUploadWorkflowAsync()
        {
            try
            {
                await ExecuteReadyToUploadWorkflowFromVmAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unobserved exception in ReadyToUpload Workflow: {ex.Message}");
                try
                {
                    SCHLStudio.App.Services.Diagnostics.AppDataLog.LogError("ExplorerV2", "SafeExecuteReadyToUploadWorkflowAsync", ex);
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"SafeExecuteReadyToUploadWorkflowAsync logging error: {logEx.Message}");
                }
            }
        }

        private async Task ExecuteReadyToUploadWorkflowFromVmAsync()
        {
            try
            {
                if (_isReadyToUploadRunning)
                {
                    return;
                }

                _isReadyToUploadRunning = true;
                var baseDir = GetActiveJobFolderPath();

                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            "Select an Active Job folder first.",
                            "SCHL App",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception msgEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"MessageBox.Show error in ExecuteReadyToUploadWorkflowFromVmAsync: {msgEx.Message}");
                    }

                    return;
                }

                var selected = FilesListView?.SelectedItems
                    ?.OfType<FileTileItem>()
                    .Select(x => (x?.FullPath ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (selected.Count == 0)
                {
                    return;
                }

                try
                {
                    if (ReadyToUploadButton is not null)
                    {
                        ReadyToUploadButton.IsEnabled = false;
                    }
                }
                catch (Exception btnEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ReadyToUploadButton set IsEnabled error: {btnEx.Message}");
                }

                await Task.Run(() => _doneMoveService.MoveToReadyToUpload(baseDir, selected));

                try
                {
                    FilesListView?.SelectedItems?.Clear();
                }
                catch (Exception clrEx)
                {
                    System.Diagnostics.Debug.WriteLine($"FilesListView clear selected items error: {clrEx.Message}");
                }

                try
                {
                    var refreshDir = string.Empty;

                    refreshDir = baseDir;

                    RefreshFileTilesForCurrentContext(refreshDir);
                }
                catch (Exception refEx)
                {
                    System.Diagnostics.Debug.WriteLine($"RefreshFileTilesForCurrentContext error after ReadyToUpload: {refEx.Message}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "ReadyToUploadButton_Click",
                        ex: ex,
                        data: new Dictionary<string, string?>
                        {
                            ["isReadyToUploadRunning"] = _isReadyToUploadRunning.ToString(),
                            ["activeJobFolderPath"] = GetActiveJobFolderPath()
                        });
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ReadyToUpload error logging failed: {logEx.Message}");
                }
            }
            finally
            {
                try
                {
                    if (ReadyToUploadButton is not null)
                    {
                        ReadyToUploadButton.IsEnabled = true;
                    }
                }
                catch (Exception finBtnEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ReadyToUploadButton finally set IsEnabled error: {finBtnEx.Message}");
                }

                try
                {
                    _isReadyToUploadRunning = false;
                }
                catch (Exception finRunEx)
                {
                    System.Diagnostics.Debug.WriteLine($"ReadyToUpload reset running flag error: {finRunEx.Message}");
                }
            }
        }

        private void ReadyToUploadButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteReadyToUploadWorkflowFromVm();
        }
    }
}
