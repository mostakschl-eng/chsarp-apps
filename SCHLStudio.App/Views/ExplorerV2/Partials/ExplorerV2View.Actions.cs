using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SCHLStudio.App.Views.ExplorerV2.Services;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private void ExecuteStartWorkflowFromVm()
        {
            if (!_vm.IsStarted)
            {
                if (!PrepareSelectionForStartAndLockMeta())
                {
                    return;
                }
            }

            _workflowService.HandleStartButtonClick(
                getIsStarted: () => _vm.IsStarted,
                setIsStarted: v => _vm.IsStarted = v,
                getIsPaused: () => _vm.IsPaused,
                setIsPaused: v => _vm.IsPaused = v,
                clearBreakReason: ClearBreakReason,
                startIdleMonitor: StartIdleMonitor,
                startWorkTimerFresh: StartWorkTimerFresh,
                trackerStartSession: TrackerStartSession,
                getTrackerTargetFullPaths: GetSelectedFullPaths,
                trackerQueueWorking: TrackerQueueWorking,
                enableActions: _vm.EnableActions,
                applyRunningStyle: () =>
                {
                    if (_cachedWarningBrush   != null) StartButton.Background = _cachedWarningBrush;
                    if (_cachedTextWhiteBrush != null) StartButton.Foreground = _cachedTextWhiteBrush;
                },
                resetIdleAutoPauseAndHideWarning: () =>
                {
                    _isIdleAutoPaused = false;
                    HideIdleWarning();
                },
                pauseWorkTimer: PauseWorkTimer,
                trackerBeginPause: TrackerBeginPause,
                trackerEndPause: TrackerEndPause,
                trackerQueueResumed: TrackerQueueResumed,
                resumeWorkTimer: ResumeWorkTimer,
                applyPausedStyle: isPaused =>
                {
                    if (isPaused)
                    {
                        if (_cachedPrimaryBrush   != null) StartButton.Background = _cachedPrimaryBrush;
                        if (_cachedTextBlackBrush != null) StartButton.Foreground = _cachedTextBlackBrush;
                    }
                    else
                    {
                        if (_cachedWarningBrush   != null) StartButton.Background = _cachedWarningBrush;
                        if (_cachedTextWhiteBrush != null) StartButton.Foreground = _cachedTextWhiteBrush;
                    }
                },
                setStartButtonText: v => _vm.StartButtonText = v,
                setActionButtonsEnabled: enabled =>
                {
                    _vm.IsFinishEnabled = enabled;
                    _vm.IsWalkOutEnabled = enabled;
                    _vm.IsSkipEnabled = enabled;
                });
        }

        private bool PrepareSelectionForStartAndLockMeta()
        {
            try
            {
                var workType = (WorkTypeButton?.Content as string) ?? string.Empty;
                var tasks = GetCheckedTasksForDrop();
                if (!EnsureWorkTypeAndTasksSelectedForDrop(workType, tasks))
                {
                    return false;
                }

                var selectedPaths = _vm.SelectedFiles
                    .Select(x => (x?.FullPath ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                if (selectedPaths.Length == 0)
                {
                    return false;
                }

                var wt = GetCurrentWorkTypeInfo();
                var wtCtx = new WorkTypeDropContext
                {
                    Name = wt.Name,
                    IsProduction = wt.IsProduction,
                    IsQc = wt.IsQc,
                    IsQc1 = wt.IsQc1,
                    IsQcAc = wt.IsQcAc,
                    IsTestFile = wt.IsTestFile,
                    IsAdditional = wt.IsAdditional,
                    IsShared = wt.IsShared,
                    IsTranning = wt.IsTranning
                };

                var requiresBaseDir = _dragDropService.RequiresBaseDirForDrop(wtCtx);
                var baseDir = ResolveBaseDirForDrop(requiresBaseDir);
                if (requiresBaseDir && string.IsNullOrWhiteSpace(baseDir))
                {
                    return false;
                }

                if (!EnsureDroppedPathsUnderBaseDir(baseDir, selectedPaths))
                {
                    return false;
                }

                var rawDisplayName = (SCHLStudio.App.Configuration.AppConfig.CurrentDisplayName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(rawDisplayName))
                {
                    rawDisplayName = FileOperationHelper.GetAppCurrentUser(this);
                }

                var userNameSafe = ExplorerV2DragDropService.GetSafeRealNameForDrop(rawDisplayName);
                var movedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var removeFromTiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var movedRows = _dragDropService.BuildDropRows(
                    selectedPaths,
                    wtCtx,
                    userNameSafe,
                    movedSourcePaths,
                    removeFromTiles);

                if (movedRows.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "Unable to prepare selected files for start.",
                        "SCHL App",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return false;
                }

                _vm.SelectedFiles.Clear();
                _vm.SelectedFilePaths.Clear();

                var serial = 1;
                foreach (var row in movedRows)
                {
                    if (row == null)
                    {
                        continue;
                    }

                    var path = (row.FullPath ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    if (_vm.SelectedFilePaths.Add(path))
                    {
                        row.Serial = serial++;
                        _vm.SelectedFiles.Add(row);
                    }
                }

                if (_vm.SelectedFiles.Count == 0)
                {
                    return false;
                }

                if ((wt.IsProduction || wt.IsQc) && !string.IsNullOrWhiteSpace(baseDir) && Directory.Exists(baseDir))
                {
                    RefreshFileTilesForCurrentContext(baseDir);
                }

                int? etValue = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(_vm.ActiveJobClientCode))
                    {
                        etValue = _jobListRows
                            .FirstOrDefault(x =>
                                string.Equals(x.ClientCode, _vm.ActiveJobClientCode, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(x.FolderPath, _vm.ActiveJobFolderPath, StringComparison.OrdinalIgnoreCase))
                            ?.ET;
                    }
                }
                catch
                {
                    etValue = null;
                }

                _vm.LockSelectionMeta(_vm.ActiveJobClientCode, workType, tasks, etValue, SCHLStudio.App.Services.Api.Tracker.ShiftDetector.GetCurrentShift());
                UpdateSelectedFilesMetaText();

                return true;
            }
            catch (Exception ex)
            {
                LogSuppressedError("PrepareSelectionForStartAndLockMeta", ex);
                return false;
            }
        }


        private void ExecuteFinishWorkflowFromVm()
        {
            _ = SafeExecuteFinishWorkflowAsync();
        }

        private async Task SafeExecuteFinishWorkflowAsync()
        {
            try
            {
                await ExecuteFinishWorkflowFromVmAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unobserved exception in Finish Workflow: {ex.Message}");
                LogSuppressedError("SafeExecuteFinishWorkflowAsync", ex);
            }
        }

        private bool CanExecuteFinish()
        {
            return _workflowService.CanExecuteFinish(
                _vm.IsStarted,
                _vm.IsPaused,
                GetSelectedFullPaths);
        }

        private List<string> GetSelectedFullPaths()
        {
            return _workflowService.GetSelectedFullPaths(
                _vm.SelectedFiles.Select(x => x?.FullPath));
        }

        private List<string> GetTrackerTargetFullPaths()
        {
            return GetSelectedFullPaths();
        }

        private async Task ExecuteFinishWorkflowFromVmAsync()
        {
            try
            {
                if (_isFinishRunning)
                {
                    return;
                }

                _isFinishRunning = true;
                if (!CanExecuteFinish())
                {
                    return;
                }

                var selected = GetSelectedFullPaths();
                var finishSnapshot = selected
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (_workflowService.HandleEmptyFinishSelection(
                    selectedCount: finishSnapshot.Count,
                    resetWorkflow: () => { },
                    clearSelection: _vm.ClearSelection,
                    updateSelectedFilesMetaText: UpdateSelectedFilesMetaText,
                    resetActionButtons: ResetActionButtons))
                {
                    return;
                }

                try
                {
                    var activeSnapshot = GetTrackerTargetFullPaths();
                    _workflowService.DispatchFinishTrackerSync(
                        getWorkTimerElapsedSeconds: GetWorkTimerElapsedSeconds,
                        getTrackerTargetFullPaths: GetSelectedFullPaths,
                        queueDoneBatch: (files, elapsed) => TrackerQueueDoneBatch(files, elapsed, activeSnapshot));
                }
                catch (Exception syncEx)
                {
                    LogSuppressedError("ExecuteFinishWorkflowFromVmAsync_TrackerSync", syncEx);
                }

                try
                {
                    var wt = GetCurrentWorkTypeInfo();
                    var effectiveWorkType = wt.Name;
                    try
                    {
                        if (_vm.HasSelectionMetaLock)
                        {
                            var locked = (_vm.SelectionLockedWorkType ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(locked))
                            {
                                effectiveWorkType = locked;
                            }
                        }
                    }
                    catch (Exception ex_safe)
                    {
                        LogSuppressedError("ExecuteFinishWorkflowFromVmAsync_EffectiveWorkType", ex_safe);
                    }

                    var baseDir = GetActiveJobFolderPath();
                    await MoveSelectedToDoneWithSingleRetryAsync(
                        baseDir,
                        effectiveWorkType,
                        finishSnapshot);
                }
                catch (Exception qcEx)
                {
                    LogSuppressedError("ExecuteFinishWorkflowFromVmAsync_QcMove", qcEx);
                }

                _workflowService.FinalizeFinishUiState(
                    resetWorkflow: () => { },
                    clearSelection: _vm.ClearSelection,
                    updateSelectedFilesMetaText: UpdateSelectedFilesMetaText,
                    resetActionButtons: ResetActionButtons);
            }
            catch (Exception ex)
            {
                LogSuppressedError("FinishButton_Click", ex);
            }
            finally
            {
                try
                {
                    _isFinishRunning = false;
                }
                catch (Exception finEx)
                {
                    LogSuppressedError("ExecuteFinishWorkflowFromVmAsync_Finally", finEx);
                }
            }
        }

        private async Task MoveSelectedToDoneWithSingleRetryAsync(
            string baseDir,
            string? effectiveWorkType,
            IReadOnlyList<string> snapshot)
        {
            var targets = (snapshot ?? Array.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targets.Count == 0)
            {
                return;
            }

            // Keep sequential move behavior; retry only remaining files once.
            await Task.Run(() => _doneMoveService.MoveToDone(targets, effectiveWorkType));

            var remaining = GetExistingPaths(targets);
            if (remaining.Count > 0)
            {
                await Task.Run(() => _doneMoveService.MoveToDone(remaining, effectiveWorkType));
            }

            try
            {
                RefreshFileTilesForCurrentContext(baseDir);
            }
            catch (Exception ex)
            {
                LogSuppressedError("MoveSelectedToDoneWithSingleRetryAsync.RefreshFileTiles", ex);
            }
        }

        private static List<string> GetExistingPaths(IEnumerable<string> filePaths)
        {
            var existing = new List<string>();
            foreach (var raw in filePaths ?? Array.Empty<string>())
            {
                try
                {
                    var path = (raw ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        existing.Add(path);
                    }
                }
                catch
                {
                }
            }

            return existing
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

    }
}
