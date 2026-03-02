using System;
using System.Collections.Generic;
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
        private void ExecuteWalkOutWorkflowFromVm()
        {
            _ = SafeExecuteWalkOutWorkflowAsync();
        }

        private async Task SafeExecuteWalkOutWorkflowAsync()
        {
            try
            {
                await ExecuteWalkOutWorkflowFromVmAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unobserved exception in WalkOut Workflow: {ex.Message}");
                LogSuppressedError("SafeExecuteWalkOutWorkflowAsync", ex);
            }
        }

        private async Task ExecuteWalkOutWorkflowFromVmAsync()
        {
            try
            {
                if (_isWalkOutRunning)
                {
                    return;
                }

                _isWalkOutRunning = true;
                if (!_vm.IsStarted)
                {
                    return;
                }

                if (_vm.IsPaused)
                {
                    return;
                }

                var selected = SelectedFilesListBox?.SelectedItems
                    .OfType<SelectedFileRow>()
                    .ToList() ?? new List<SelectedFileRow>();

                if (selected.Count == 0)
                {
                    System.Windows.MessageBox.Show("Select file(s) first.", "Walkout", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var baseDir = GetActiveJobFolderPath();

                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
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
                catch (Exception usrEx)
                {
                    System.Diagnostics.Debug.WriteLine($"WalkOut get current user error: {usrEx.Message}");
                    user = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(user))
                {
                    await Task.Run(() => _doneMoveService.MoveBackFromWorkFoldersToParent(filePaths, user));
                }
                else
                {
                    await Task.Run(() => _doneMoveService.MoveBackFromWorkFoldersToParent(filePaths, null));
                }

                TrackerQueueWalkOut(filePaths, GetWorkTimerElapsedSeconds(), activeSnapshot);
                PostActionCleanup(baseDir);

                if (_vm.SelectedFilePaths.Count == 0)
                {
                    ResetActionButtons();
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("WalkOutButton_Click", ex);
            }
            finally
            {
                try
                {
                    _isWalkOutRunning = false;
                }
                catch (Exception finEx)
                {
                    System.Diagnostics.Debug.WriteLine($"WalkOut finally block error: {finEx.Message}");
                }
            }
        }

    }
}
