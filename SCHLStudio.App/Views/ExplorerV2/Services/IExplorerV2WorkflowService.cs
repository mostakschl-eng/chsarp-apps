using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal interface IExplorerV2WorkflowService
    {
        void HandleStartButtonClick(
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
            Action<bool> setActionButtonsEnabled);

        bool CanExecuteFinish(bool isStarted, bool isPaused, Func<List<string>> getSelectedFullPaths);

        List<string> GetSelectedFullPaths(IEnumerable<string?> fullPaths);

        Task MoveSelectedToDoneAndRefreshAsync(
            string baseDir,
            Func<List<string>> getSelectedFullPaths,
            Action<string> refreshFileTilesForCurrentContext,
            Action<IEnumerable<string>> moveAction);

        void DispatchFinishTrackerSync(
            Func<int> getWorkTimerElapsedSeconds,
            Func<List<string>> getTrackerTargetFullPaths,
            Action<IReadOnlyList<string>, int> queueDoneBatch);

        void FinalizeFinishUiState(
            Action resetWorkflow,
            Action clearSelection,
            Action updateSelectedFilesMetaText,
            Action resetActionButtons);

        bool HandleEmptyFinishSelection(
            int selectedCount,
            Action resetWorkflow,
            Action clearSelection,
            Action updateSelectedFilesMetaText,
            Action resetActionButtons);
    }
}
