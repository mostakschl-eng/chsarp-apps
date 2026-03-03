using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.Views.ExplorerV2.Models;
using SCHLStudio.App.Views.ExplorerV2.Services;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private void SetActiveJob(string? clientCode, string? folderPath, string? taskRaw)
        {
            try
            {
                var previousClient = (_vm.ActiveJobClientCode ?? string.Empty).Trim();
                var previousFolder = (_vm.ActiveJobFolderPath ?? string.Empty).Trim();

                var jobChanged = false;

                _vm.ActiveJobClientCode = string.IsNullOrWhiteSpace(clientCode) ? null : clientCode.Trim();
                _vm.ActiveJobFolderPath = string.IsNullOrWhiteSpace(folderPath) ? null : folderPath.Trim();
                _vm.ActiveJobTaskRaw = string.IsNullOrWhiteSpace(taskRaw) ? null : taskRaw.Trim();

                try
                {
                    var currentClient = (_vm.ActiveJobClientCode ?? string.Empty).Trim();
                    var currentFolder = (_vm.ActiveJobFolderPath ?? string.Empty).Trim();
                    jobChanged = !string.Equals(previousClient, currentClient, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(previousFolder, currentFolder, StringComparison.OrdinalIgnoreCase);

                    if (!_vm.IsStarted && jobChanged && _vm.SelectedFiles.Count > 0)
                    {
                        _vm.ClearSelection();
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                try
                {
                    if (FilesWorkButton is not null)
                    {
                        FilesWorkButton.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                    }

                    FilesProductionDoneButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                    FilesQc1DoneButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                    FilesQc2DoneButton?.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }



                _activeJobTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(_vm.ActiveJobTaskRaw))
                {
                    foreach (var t in _vm.ActiveJobTaskRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!string.IsNullOrWhiteSpace(t))
                        {
                            var token = t.Trim();
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                _activeJobTasks.Add(token);
                            }

                            foreach (var part in token.Split(new[] { '+', '&', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                var p = (part ?? string.Empty).Trim();
                                if (!string.IsNullOrWhiteSpace(p))
                                {
                                    _activeJobTasks.Add(p);
                                }
                            }
                        }
                    }
                }

                try
                {
                    if (!_vm.IsStarted && jobChanged)
                    {
                        ApplyActiveJobTasksToMenuSelections();
                        UpdateTaskButtonHeader();
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                UpdateTaskMenuActiveHighlight();
                UpdateSelectedFilesMetaText();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }

        private void ApplyActiveJobTasksToMenuSelections()
        {
            try
            {
                if (TaskMenu is null)
                {
                    return;
                }

                foreach (var mi in TaskMenu.Items.OfType<MenuItem>())
                {
                    if (mi is null)
                    {
                        continue;
                    }

                    mi.IsChecked = false;
                }

                if (_activeJobTasks.Count == 0)
                {
                    return;
                }

                foreach (var mi in TaskMenu.Items.OfType<MenuItem>())
                {
                    if (mi is null)
                    {
                        continue;
                    }

                    var header = (mi.Header?.ToString() ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(header) && _activeJobTasks.Contains(header))
                    {
                        mi.IsChecked = true;
                    }
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }



        private CancellationTokenSource? _jobListLoadCts;

        private IApiClient? TryGetApiClient()
        {
            return GetNonCritical(() =>
                (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(IApiClient)) as IApiClient,
                null);
        }

        private async Task LoadJobListFromApiAsync(JobListWindow? windowToUpdate)
        {
            try
            {
                RunNonCritical(() => _jobListLoadCts?.Cancel());
                RunNonCritical(() => _jobListLoadCts?.Dispose());

                _jobListLoadCts = new CancellationTokenSource();
                var ct = _jobListLoadCts.Token;

                var api = TryGetApiClient();
                if (api is null || !api.IsAuthenticated)
                {
                    return;
                }

                static async Task<string> FetchJobListWithRetryAsync(IApiClient apiClient, CancellationToken token)
                {
                    var first = await apiClient.GetJobListAsync(null, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    if (!string.IsNullOrWhiteSpace(first))
                    {
                        return first;
                    }

                    try
                    {
                        await Task.Delay(500, token).ConfigureAwait(false);
                    }
                    catch
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    var second = await apiClient.GetJobListAsync(null, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return second;
                }

                var json = await FetchJobListWithRetryAsync(api, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                JobListApiResponse? data;
                try
                {
                    data = JsonSerializer.Deserialize<JobListApiResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    data = null;
                }

                if (data is null || data.Jobs is null)
                {
                    return;
                }

                var rows = data.Jobs
                    .Where(j => j is not null)
                    .Select(j => new JobListRow
                    {
                        ClientCode = (j.ClientCode ?? string.Empty).Trim(),
                        FolderPath = (j.FolderPath ?? j.Folder ?? string.Empty).Trim(),
                        Status = (j.Status ?? string.Empty).Trim(),
                        Type = (j.Type ?? string.Empty).Trim(),
                        ET = j.Et,
                        NOF = j.Nof,
                        Task = (j.Task ?? string.Empty).Trim(),
                        IsAdded = string.Equals((j.ClientCode ?? string.Empty).Trim(), _vm.ActiveJobClientCode ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals((j.FolderPath ?? j.Folder ?? string.Empty).Trim(), _vm.ActiveJobFolderPath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    })
                    .Where(r => !string.IsNullOrWhiteSpace(r.ClientCode) && !string.IsNullOrWhiteSpace(r.FolderPath))
                    .ToList();

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _jobListRows.Clear();
                        _jobListRows.AddRange(rows);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }

                    try
                    {
                        windowToUpdate?.ReplaceRows(_jobListRows);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }

                    // Also refresh the inline Job List panel if open
                    try
                    {
                        JlpRefreshAfterDataChange();
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }
                });
            }
            catch (OperationCanceledException ex)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex);
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }

        private void JobListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Toggle the inline Job List panel instead of opening a separate window
                ToggleJobListPanel();

                // Kick off a fresh API load whenever the panel opens
                if (_isJobListPanelOpen)
                {
                    try
                    {
                        _ = LoadJobListFromApiAsync(null);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }


    }
}
