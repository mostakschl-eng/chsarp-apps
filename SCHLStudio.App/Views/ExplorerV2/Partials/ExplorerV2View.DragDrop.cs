using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using SCHLStudio.App.ViewModels.Windows;
using SCHLStudio.App.Views.ExplorerV2.Models;
using SCHLStudio.App.Views.ExplorerV2.Services;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private readonly ExplorerV2DragDropService _dragDropService = new ExplorerV2DragDropService();

        private void SelectedFilesListBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                var isInternalDrag = e.Data?.GetDataPresent("SCHLStudio_InternalDrag") == true;
                e.Effects = isInternalDrag && e.Data?.GetDataPresent(System.Windows.DataFormats.FileDrop) == true
                    ? System.Windows.DragDropEffects.Copy
                    : System.Windows.DragDropEffects.None;
                e.Handled = true;
            }
            catch (Exception dragEx)
            {
                System.Diagnostics.Debug.WriteLine($"SelectedFilesListBox_DragOver error: {dragEx.Message}");
            }
        }

        private async void SelectedFilesListBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                try
                {
                    e.Handled = true;
                }
                catch
                {
                }

                if (e.Data?.GetDataPresent(System.Windows.DataFormats.FileDrop) != true)
                {
                    return;
                }

                if (e.Data?.GetDataPresent("SCHLStudio_InternalDrag") != true)
                {
                    System.Windows.MessageBox.Show(
                        "You can only drag files from the left file grid.",
                        "SCHL App",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (_vm.IsStarted)
                {
                    System.Windows.MessageBox.Show(
                        "Finish current files before adding new ones.",
                        "SCHL App",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var workType = (WorkTypeButton?.Content as string) ?? string.Empty;
                var tasks = GetCheckedTasksForDrop();
                if (!EnsureWorkTypeAndTasksSelectedForDrop(workType, tasks))
                {
                    return;
                }

                if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
                {
                    return;
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

                if (_dragDropService.RequiresMaxFilesLimit(wtCtx))
                {
                    if (!ApplyLimitedWorkTypeDropLimit(wt.Name, ref paths))
                    {
                        return;
                    }
                }

                var requiresBaseDir = _dragDropService.RequiresBaseDirForDrop(wtCtx);

                var activeJobBaseDir = string.Empty;
                try
                {
                    activeJobBaseDir = GetActiveJobFolderPath();
                }
                catch
                {
                    activeJobBaseDir = string.Empty;
                }

                var ctxPath = (_filesContextPath ?? string.Empty).Trim();
                var jobFolderCandidates = new List<string>();
                try
                {
                    jobFolderCandidates = _jobListRows
                        .Select(x => (x?.FolderPath ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch
                {
                    jobFolderCandidates = new List<string>();
                }

                (string BaseDir, bool PathsUnderBase) validation;
                try
                {
                    validation = await Task.Run(() =>
                    {
                        string resolvedBaseDir = string.Empty;

                        if (requiresBaseDir)
                        {
                            var candidate = (activeJobBaseDir ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                            {
                                resolvedBaseDir = candidate;
                            }
                            else
                            {
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(ctxPath) && Directory.Exists(ctxPath))
                                    {
                                        foreach (var p in jobFolderCandidates)
                                        {
                                            try
                                            {
                                                if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
                                                {
                                                    continue;
                                                }

                                                if (FileOperationHelper.IsSameOrUnderPath(p, ctxPath))
                                                {
                                                    resolvedBaseDir = p;
                                                    break;
                                                }
                                            }
                                            catch
                                            {
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }

                        var under = true;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(resolvedBaseDir) && Directory.Exists(resolvedBaseDir))
                            {
                                under = _dragDropService.AreAllDroppedPathsUnderBaseDir(resolvedBaseDir, paths);
                            }
                        }
                        catch
                        {
                            under = true;
                        }

                        return (resolvedBaseDir, under);
                    });
                }
                catch
                {
                    validation = (string.Empty, true);
                }

                var baseDir = (validation.BaseDir ?? string.Empty).Trim();
                if (requiresBaseDir && string.IsNullOrWhiteSpace(baseDir))
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            "Select an Active Job folder first.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }
                    return;
                }

                if (!validation.PathsUnderBase)
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            "Dropped files are outside the current Active Job folder. Please select the correct job.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }
                    return;
                }

                var selectionBeforeDrop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    selectionBeforeDrop = new HashSet<string>(_vm.SelectedFilePaths, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    selectionBeforeDrop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                var movedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                List<SelectedFileRow> toAddRows;
                try
                {
                    toAddRows = await Task.Run(() => _dragDropService.BuildTemporaryDropRows(paths));
                }
                catch
                {
                    toAddRows = new List<SelectedFileRow>();
                }

                HashSet<string> _unused;
                var added = ApplyDropRowsToSelection(toAddRows, movedSourcePaths, out _unused);

                var newlyAdded = new List<string>();
                try
                {
                    newlyAdded = _vm.SelectedFilePaths
                        .Where(x => !selectionBeforeDrop.Contains(x))
                        .ToList();
                }
                catch
                {
                    newlyAdded = new List<string>();
                }

                try
                {
                    if (added)
                    {
                        // Temporary drop only: no move/copy operation and no tile mutation until Start.
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                }

                // No need to handle IsStarted here — drops are blocked while started.

                try
                {
                    var selectedItems = SelectedFilesListBox?.SelectedItems.OfType<SelectedFileRow>().ToList()
                        ?? new List<SelectedFileRow>();
                    _vm.UpdateHighlightedFiles(selectedItems);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                }

                UpdateSelectedFilesMetaText();
            }
            catch (Exception dropEx)
            {
                LogSuppressedError("SelectedFilesListBox_Drop", dropEx);
            }
        }

        private bool ApplyLimitedWorkTypeDropLimit(string? workTypeName, ref string[] paths)
        {
            try
            {
                var max = 5;
                try
                {
                    max = _dragDropService.GetMaxFilesPerUserOrDefault();
                }
                catch
                {
                    max = 5;
                }

                var remainingSlots = Math.Max(0, max - _vm.SelectedFiles.Count);
                if (remainingSlots == 0)
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            (string.IsNullOrWhiteSpace(workTypeName) ? "This work type" : workTypeName.Trim())
                                + " allows maximum " + max + " files. Remove files from Selected Files to add more.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }
                    return false;
                }

                if (paths.Length > remainingSlots)
                {
                    paths = paths.Take(remainingSlots).ToArray();
                    try
                    {
                        System.Windows.MessageBox.Show(
                            (string.IsNullOrWhiteSpace(workTypeName) ? "This work type" : workTypeName.Trim())
                                + " allows maximum " + max + " files. Only the first " + remainingSlots + " file(s) were added.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }
                }

                return true;
            }
            catch (Exception limitEx)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyLimitedWorkTypeDropLimit error: {limitEx.Message}");
                return true;
            }
        }

        private List<string> GetCheckedTasksForDrop()
        {
            try
            {
                return TaskMenu?.Items.OfType<System.Windows.Controls.MenuItem>()
                    .Where(x => x.IsChecked)
                    .Select(x => (x.Header as string) ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList() ?? new List<string>();
            }
            catch (Exception chkEx)
            {
                System.Diagnostics.Debug.WriteLine($"GetCheckedTasksForDrop error: {chkEx.Message}");
                return new List<string>();
            }
        }

        private bool EnsureWorkTypeAndTasksSelectedForDrop(string? workType, IReadOnlyList<string> tasks)
        {
            try
            {
                var wt = (workType ?? string.Empty);
                var hasWorkType = !string.IsNullOrWhiteSpace(wt) && !string.Equals(wt, "Work Type", StringComparison.OrdinalIgnoreCase);
                if (hasWorkType && tasks.Count > 0)
                {
                    return true;
                }

                try
                {
                    _vm.SelectedFilesMetaText = "Select Work Type and Task first.";
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                }

                try
                {
                    System.Windows.MessageBox.Show(
                        "Select Work Type and Task first.",
                        "SCHL App",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                }

                return false;
            }
            catch (Exception wsEx)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureWorkTypeAndTasksSelectedForDrop error: {wsEx.Message}");
                return false;
            }
        }

        private string ResolveBaseDirForDrop(bool requiresBaseDir)
        {
            try
            {
                if (!requiresBaseDir)
                {
                    return string.Empty;
                }

                var baseDir = string.Empty;
                try
                {
                    baseDir = GetActiveJobFolderPath();
                }
                catch
                {
                    baseDir = string.Empty;
                }

                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    try
                    {
                        var ctx = (_filesContextPath ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(ctx) && Directory.Exists(ctx))
                        {
                            baseDir = _jobListRows
                                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?.FolderPath)
                                    && Directory.Exists(x.FolderPath)
                                    && FileOperationHelper.IsSameOrUnderPath(x.FolderPath, ctx))
                                ?.FolderPath ?? string.Empty;
                        }
                    }
                    catch
                    {
                        baseDir = string.Empty;
                    }
                }

                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            "Select an Active Job folder first.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }

                    return string.Empty;
                }

                return baseDir;
            }
            catch (Exception rbdEx)
            {
                System.Diagnostics.Debug.WriteLine($"ResolveBaseDirForDrop error: {rbdEx.Message}");
                return string.Empty;
            }
        }

        private bool EnsureDroppedPathsUnderBaseDir(string baseDir, IEnumerable<string> paths)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    return true;
                }

                var allUnderActive = true;
                try
                {
                    allUnderActive = _dragDropService.AreAllDroppedPathsUnderBaseDir(baseDir, paths);
                }
                catch
                {
                    allUnderActive = true;
                }

                if (!allUnderActive)
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            "Dropped files are outside the current Active Job folder. Please select the correct job.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }

                    return false;
                }

                return true;
            }
            catch (Exception udpEx)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureDroppedPathsUnderBaseDir error: {udpEx.Message}");
                return true;
            }
        }

        private bool ApplyDropRowsToSelection(
            IEnumerable<SelectedFileRow> toAddRows,
            HashSet<string> movedSourcePaths,
            out HashSet<string> removeFromTiles)
        {
            var added = false;
            removeFromTiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (movedSourcePaths.Count > 0)
                {
                    try
                    {
                        foreach (var src in movedSourcePaths)
                        {
                            _vm.SelectedFilePaths.Remove(src);
                        }

                        for (var i = _vm.SelectedFiles.Count - 1; i >= 0; i--)
                        {
                            var p = (_vm.SelectedFiles[i]?.FullPath ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(p) && movedSourcePaths.Contains(p))
                            {
                                _vm.SelectedFiles.RemoveAt(i);
                            }
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
                    }
                }

                foreach (var row in toAddRows)
                {
                    if (row is null)
                    {
                        continue;
                    }

                    var finalPath = (row.FullPath ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(finalPath))
                    {
                        continue;
                    }

                    if (!_vm.SelectedFilePaths.Add(finalPath))
                    {
                        continue;
                    }

                    row.Serial = _vm.SelectedFiles.Count + 1;
                    _vm.SelectedFiles.Add(row);
                    added = true;
                }
            }
            catch (Exception adrEx)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyDropRowsToSelection error: {adrEx.Message}");
            }

            try
            {
                _vm.StartCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
            }

            try
            {
                _vm.BreakCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
            }

            try
            {
                _vm.SkipCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.DragDrop", ex_safe_log);
            }

            return added;
        }
    }
}
