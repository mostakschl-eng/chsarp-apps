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
                    var bg = TryFindResource("WarningBrush") as System.Windows.Media.Brush;
                    var fg = TryFindResource("TextWhiteBrush") as System.Windows.Media.Brush;
                    if (bg != null) StartButton.Background = bg;
                    if (fg != null) StartButton.Foreground = fg;
                },
                resetIdleAutoPauseAndHideWarning: () =>
                {
                    _isIdleAutoPaused = false;
                    HideIdleWarning();
                },
                pauseWorkTimer: PauseWorkTimer,
                trackerBeginPause: TrackerBeginPause,
                trackerQueuePaused: TrackerQueuePaused,
                trackerEndPause: TrackerEndPause,
                trackerQueueResumed: TrackerQueueResumed,
                resumeWorkTimer: ResumeWorkTimer,
                applyPausedStyle: isPaused =>
                {
                    if (isPaused)
                    {
                        var bg = TryFindResource("PrimaryBrush") as System.Windows.Media.Brush;
                        var fg = TryFindResource("TextBlackBrush") as System.Windows.Media.Brush;
                        if (bg != null) StartButton.Background = bg;
                        if (fg != null) StartButton.Foreground = fg;
                    }
                    else
                    {
                        var bg = TryFindResource("WarningBrush") as System.Windows.Media.Brush;
                        var fg = TryFindResource("TextWhiteBrush") as System.Windows.Media.Brush;
                        if (bg != null) StartButton.Background = bg;
                        if (fg != null) StartButton.Foreground = fg;
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


        private async Task MoveSelectedToDoneAndRefreshAsync(string baseDir, Action<IEnumerable<string>> moveAction)
        {
            await _workflowService.MoveSelectedToDoneAndRefreshAsync(
                baseDir,
                GetSelectedFullPaths,
                RefreshFileTilesForCurrentContext,
                moveAction);
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
                if (_workflowService.HandleEmptyFinishSelection(
                    selectedCount: selected.Count,
                    resetWorkflow: () => { },
                    clearSelection: _vm.ClearSelection,
                    updateSelectedFilesMetaText: UpdateSelectedFilesMetaText,
                    resetActionButtons: ResetActionButtons))
                {
                    return;
                }

                try
                {
                    var wt = GetCurrentWorkTypeInfo();
                    var baseDir = GetActiveJobFolderPath();

                    await MoveSelectedToDoneAndRefreshAsync(
                        baseDir,
                        filePaths => _doneMoveService.MoveToDone(filePaths, wt.Name));
                }
                catch (Exception qcEx)
                {
                    LogSuppressedError("ExecuteFinishWorkflowFromVmAsync_QcMove", qcEx);
                }

                try
                {
                    var wt2 = GetCurrentWorkTypeInfo();

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

    }
}
