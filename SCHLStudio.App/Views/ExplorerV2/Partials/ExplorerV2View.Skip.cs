using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SCHLStudio.App.Views.ExplorerV2.Models;
using SCHLStudio.App.Views.ExplorerV2.Services;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private async void ExecuteSkipWorkflowFromVm()
        {
            try
            {
                if (_isSkipRunning)
                {
                    return;
                }

                _isSkipRunning = true;
                if (_vm.IsPaused)
                {
                    return;
                }

                var wt = GetCurrentWorkTypeInfo();
                _ = wt.IsProduction;

                var baseDir = GetActiveJobFolderPath();

                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    return;
                }

                var selected = SelectedFilesListBox?.SelectedItems
                    .OfType<SelectedFileRow>()
                    .ToList() ?? new System.Collections.Generic.List<SelectedFileRow>();

                if (selected.Count == 0)
                {
                    System.Windows.MessageBox.Show("Select file(s) first.", "Skip", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var filePaths = selected
                    .Select(x => (x?.FullPath ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var activeSnapshot = GetTrackerTargetFullPaths();
                
                foreach (var path in filePaths)
                {
                    RemoveFileFromSelection(path);
                }

                var user = string.Empty;
                try
                {
                    user = GetAppCurrentUser();
                }
                catch (Exception ex)
                {
                    LogSuppressedError("ExecuteSkipWorkflowFromVm.GetAppCurrentUser", ex);
                    user = string.Empty;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(user))
                    {
                        await Task.Run(() => _doneMoveService.MoveBackFromWorkFoldersToParent(filePaths, user));
                    }
                    else
                    {
                        await Task.Run(() => _doneMoveService.MoveBackFromWorkFoldersToParent(filePaths, null));
                    }
                }
                catch (Exception ex)
                {
                    LogSuppressedError("ExecuteSkipWorkflowFromVm.MoveBackFromWorkFoldersToParent", ex);
                }

                try
                {
                    TrackerQueueSkip(filePaths, GetWorkTimerElapsedSeconds(), activeSnapshot);
                }
                catch (Exception ex)
                {
                    LogSuppressedError("ExecuteSkipWorkflowFromVm.TrackerQueueSkip", ex);
                }

                PostActionCleanup(baseDir);

                if (_vm.SelectedFilePaths.Count == 0)
                {
                    ResetActionButtons();
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("SkipButton_Click", ex);
            }
            finally
            {
                try
                {
                    _isSkipRunning = false;
                }
                catch (Exception ex)
                {
                    LogSuppressedError("ExecuteSkipWorkflowFromVm.Finally", ex);
                }
            }
        }

    }
}
