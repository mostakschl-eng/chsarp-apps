using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal sealed class ExplorerV2WorkflowService : IExplorerV2WorkflowService
    {
        public void HandleStartButtonClick(
            Func<bool> getIsStarted,
            Action<bool> setIsStarted,
            Func<bool> getIsPaused,
            Action<bool> setIsPaused,
            Action clearBreakReason,
            Action startIdleMonitor,
            Action startWorkTimerFresh,
            Action trackerStartSession,
            Func<List<string>> getTrackerTargetFullPaths,
            Action<IReadOnlyList<string>> trackerQueueWorking,
            Action enableActions,
            Action applyRunningStyle,
            Action resetIdleAutoPauseAndHideWarning,
            Action pauseWorkTimer,
            Action trackerBeginPause,
            Action trackerEndPause,
            Action<IReadOnlyList<string>> trackerQueueResumed,
            Action resumeWorkTimer,
            Action<bool> applyPausedStyle,
            Action<string> setStartButtonText,
            Action<bool> setActionButtonsEnabled)
        {
            ExplorerV2ActionWorkflowHelper.HandleStartButtonClick(
                getIsStarted,
                setIsStarted,
                getIsPaused,
                setIsPaused,
                clearBreakReason,
                startIdleMonitor,
                startWorkTimerFresh,
                trackerStartSession,
                getTrackerTargetFullPaths,
                trackerQueueWorking,
                enableActions,
                applyRunningStyle,
                resetIdleAutoPauseAndHideWarning,
                pauseWorkTimer,
                trackerBeginPause,
                trackerEndPause,
                trackerQueueResumed,
                resumeWorkTimer,
                applyPausedStyle,
                setStartButtonText,
                setActionButtonsEnabled);
        }

        public bool CanExecuteFinish(bool isStarted, bool isPaused, Func<List<string>> getSelectedFullPaths)
            => ExplorerV2ActionWorkflowHelper.CanExecuteFinish(isStarted, isPaused, getSelectedFullPaths);

        public List<string> GetSelectedFullPaths(IEnumerable<string?> fullPaths)
            => ExplorerV2ActionWorkflowHelper.GetSelectedFullPaths(fullPaths);

        public Task MoveSelectedToDoneAndRefreshAsync(
            string baseDir,
            Func<List<string>> getSelectedFullPaths,
            Action<string> refreshFileTilesForCurrentContext,
            Action<IEnumerable<string>> moveAction)
            => ExplorerV2ActionWorkflowHelper.MoveSelectedToDoneAndRefreshAsync(
                baseDir,
                getSelectedFullPaths,
                refreshFileTilesForCurrentContext,
                moveAction);

        public void DispatchFinishTrackerSync(
            Func<int> getWorkTimerElapsedSeconds,
            Func<List<string>> getTrackerTargetFullPaths,
            Action<IReadOnlyList<string>, int> queueDoneBatch)
            => ExplorerV2ActionWorkflowHelper.DispatchFinishTrackerSync(
                getWorkTimerElapsedSeconds,
                getTrackerTargetFullPaths,
                queueDoneBatch);

        public void FinalizeFinishUiState(
            Action resetWorkflow,
            Action clearSelection,
            Action updateSelectedFilesMetaText,
            Action resetActionButtons)
            => ExplorerV2ActionWorkflowHelper.FinalizeFinishUiState(
                resetWorkflow,
                clearSelection,
                updateSelectedFilesMetaText,
                resetActionButtons);

        public bool HandleEmptyFinishSelection(
            int selectedCount,
            Action resetWorkflow,
            Action clearSelection,
            Action updateSelectedFilesMetaText,
            Action resetActionButtons)
            => ExplorerV2ActionWorkflowHelper.HandleEmptyFinishSelection(
                selectedCount,
                resetWorkflow,
                clearSelection,
                updateSelectedFilesMetaText,
                resetActionButtons);
    }
}
