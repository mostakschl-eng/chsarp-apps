using SCHLStudio.App.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal static class ExplorerV2ActionWorkflowHelper
    {
        internal static void HandleStartButtonClick(
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
            try
            {
                if (!getIsStarted())
                {
                    setIsStarted(true);
                    setIsPaused(false);
                    InvokeSafe(clearBreakReason);

                    InvokeSafe(startIdleMonitor);
                    InvokeSafe(startWorkTimerFresh);

                    InvokeSafe(trackerStartSession);
                    InvokeSafe(() => trackerQueueWorking(getTrackerTargetFullPaths()));

                    setStartButtonText("Start");
                    InvokeSafe(enableActions);
                    InvokeSafe(applyRunningStyle);
                    return;
                }

                // Start button should not pause/resume. Pausing is done via Break only.
                return;
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(HandleStartButtonClick), ex);
            }
        }

        internal static bool HandleEmptyFinishSelection(
            int selectedCount,
            Action resetWorkflow,
            Action clearSelection,
            Action updateSelectedFilesMetaText,
            Action resetActionButtons)
        {
            try
            {
                if (selectedCount > 0)
                {
                    return false;
                }

                FinalizeFinishUiState(
                    resetWorkflow,
                    clearSelection,
                    updateSelectedFilesMetaText,
                    resetActionButtons);

                return true;
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(HandleEmptyFinishSelection), ex);
                return false;
            }
        }

        internal static bool CanExecuteFinish(bool isStarted, bool isPaused, Func<List<string>> getSelectedFullPaths)
        {
            try
            {
                if (!isStarted)
                {
                    return false;
                }

                if (isPaused)
                {
                    return false;
                }

                var selected = getSelectedFullPaths();
                return selected.Count > 0;
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(CanExecuteFinish), ex);
                return false;
            }
        }

        internal static List<string> GetSelectedFullPaths(IEnumerable<string?> fullPaths)
        {
            try
            {
                var list = new List<string>();
                foreach (var item in fullPaths)
                {
                    var path = (item ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        list.Add(path);
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(GetSelectedFullPaths), ex);
                return new List<string>();
            }
        }

        internal static async Task MoveSelectedToDoneAndRefreshAsync(
            string baseDir,
            Func<List<string>> getSelectedFullPaths,
            Action<string> refreshFileTilesForCurrentContext,
            Action<IEnumerable<string>> moveAction)
        {
            try
            {
                var filePaths = getSelectedFullPaths();

                await Task.Run(() => moveAction(filePaths));

                try
                {
                    refreshFileTilesForCurrentContext(baseDir);
                }
                catch (Exception ex)
                {
                    LogSuppressed("MoveSelectedToDoneAndRefreshAsync.RefreshFileTiles", ex);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "MoveSelectedToDoneAndRefreshAsync",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["baseDir"] = baseDir,
                            ["selectedCount"] = getSelectedFullPaths().Count.ToString()
                        });
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "ExplorerV2ActionWorkflowHelper", ex_safe_log);
                }
            }
        }

        internal static void DispatchFinishTrackerSync(
            Func<int> getWorkTimerElapsedSeconds,
            Func<List<string>> getTrackerTargetFullPaths,
            Action<IReadOnlyList<string>, int> queueDoneBatch)
        {
            try
            {
                var elapsed = getWorkTimerElapsedSeconds();
                var selectedForTracker = getTrackerTargetFullPaths();

                if (selectedForTracker.Count > 0)
                {
                    queueDoneBatch(selectedForTracker, elapsed);
                }
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(DispatchFinishTrackerSync), ex);
            }
        }

        internal static void FinalizeFinishUiState(
            Action resetWorkflow,
            Action clearSelection,
            Action updateSelectedFilesMetaText,
            Action resetActionButtons)
        {
            try
            {
                resetWorkflow();
            }
            catch (Exception ex)
            {
                LogSuppressed("FinalizeFinishUiState.ResetWorkflow", ex);
            }

            try
            {
                clearSelection();
            }
            catch (Exception ex)
            {
                LogSuppressed("FinalizeFinishUiState.ClearSelection", ex);
            }

            try
            {
                updateSelectedFilesMetaText();
            }
            catch (Exception ex)
            {
                LogSuppressed("FinalizeFinishUiState.UpdateSelectedFilesMetaText", ex);
            }

            resetActionButtons();
        }

        private static void InvokeSafe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogSuppressed("InvokeSafe", ex);
            }
        }

        private static void LogSuppressed(string operation, Exception ex)
        {
            try
            {
                AppDataLog.LogError(
                    area: "ExplorerV2",
                    operation: "WorkflowHelper." + (operation ?? string.Empty),
                    ex: ex);
            }
            catch (Exception ex_safe_log)
            {
                NonCriticalLog.EnqueueError("ExplorerV2", "ExplorerV2ActionWorkflowHelper", ex_safe_log);
            }
        }
    }
}
