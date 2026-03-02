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
                _vm.ActiveJobClientCode = string.IsNullOrWhiteSpace(clientCode) ? null : clientCode.Trim();
                _vm.ActiveJobFolderPath = string.IsNullOrWhiteSpace(folderPath) ? null : folderPath.Trim();
                _vm.ActiveJobTaskRaw = string.IsNullOrWhiteSpace(taskRaw) ? null : taskRaw.Trim();

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

                try
                {
                    if (ClientCodeTextBox is not null)
                    {
                        if (_vm.HasSelectionMetaLock && !string.IsNullOrWhiteSpace(_vm.SelectionLockedClientCode))
                        {
                            ClientCodeTextBox.Text = _vm.SelectionLockedClientCode;
                        }
                        else if (!string.IsNullOrWhiteSpace(_vm.ActiveJobClientCode))
                        {
                            ClientCodeTextBox.Text = _vm.ActiveJobClientCode;
                        }
                    }
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

                UpdateTaskMenuActiveHighlight();
                UpdateSelectedFilesMetaText();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }

        private void ClientCodeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ClientCodeTextBox is null)
                {
                    return;
                }

                var t = (ClientCodeTextBox.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_vm.ActiveJobClientCode))
                {
                    ClientCodeTextBox.Text = _vm.ActiveJobClientCode;
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
                var w = new JobListWindow(_jobListRows);
                try
                {
                    _ = LoadJobListFromApiAsync(w);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                w.ReloadRequested += () =>
                {
                    try
                    {
                        _ = LoadJobListFromApiAsync(w);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }
                };
                w.AddRequested += row =>
                {
                    try
                    {
                        try
                        {
                            if (ClientCodeTextBox is not null)
                            {
                                ClientCodeTextBox.Text = row.ClientCode ?? string.Empty;
                            }
                        }
                        catch (Exception ex_safe_log)
                        {
                            LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                        }

                        if (string.IsNullOrWhiteSpace(row.FolderPath))
                        {
                            return;
                        }

                        if (!Directory.Exists(row.FolderPath))
                        {
                            return;
                        }

                        SetActiveJob(row.ClientCode, row.FolderPath, row.Task);

                        RefreshFileTilesForCurrentContext(row.FolderPath);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }
                };
                w.RemoveRequested += rowToDrop =>
                {
                    try
                    {
                        SetActiveJob(null, null, null);
                        try
                        {
                            _vm.ReplaceFileTiles(Array.Empty<FileTileItem>());
                        }
                        catch (Exception ex_safe_log)
                        {
                            LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }
                };
                try
                {
                    w.Owner = Window.GetWindow(this);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                w.Show();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }

        private void ClientCodeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key != System.Windows.Input.Key.Enter)
                {
                    return;
                }

                var code = (ClientCodeTextBox.Text ?? string.Empty).Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(code))
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(_vm.ActiveJobClientCode) && ClientCodeTextBox is not null)
                        {
                            ClientCodeTextBox.Text = _vm.ActiveJobClientCode;
                            e.Handled = true;
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }

                    try
                    {
                        var activeJobBaseDir = GetActiveJobFolderPath();

                        if (!string.IsNullOrWhiteSpace(activeJobBaseDir) && Directory.Exists(activeJobBaseDir))
                        {
                            RefreshFileTilesForCurrentContext(activeJobBaseDir);
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                    }

                    return;
                }

                try
                {
                    if (int.TryParse(code, out var index) && index > 0 && index <= _jobListRows.Count)
                    {
                        var row = _jobListRows[index - 1];
                        if (row is not null && !string.IsNullOrWhiteSpace(row.FolderPath) && Directory.Exists(row.FolderPath))
                        {
                            SetActiveJob(row.ClientCode, row.FolderPath, row.Task);
                            RefreshFileTilesForCurrentContext(row.FolderPath);
                            e.Handled = true;
                            return;
                        }
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                try
                {
                    var row = _jobListRows.FirstOrDefault(x => string.Equals(x.ClientCode, code, StringComparison.OrdinalIgnoreCase));
                    if (row is not null && !string.IsNullOrWhiteSpace(row.FolderPath) && Directory.Exists(row.FolderPath))
                    {
                        SetActiveJob(row.ClientCode, row.FolderPath, row.Task);
                        RefreshFileTilesForCurrentContext(row.FolderPath);
                        e.Handled = true;
                        return;
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                try
                {
                    if (File.Exists(code))
                    {
                        var dir = Path.GetDirectoryName(code);
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            code = dir;
                        }
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                try
                {
                    var row = _jobListRows.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x?.FolderPath)
                        && string.Equals(x.FolderPath.Trim(), code, StringComparison.OrdinalIgnoreCase));

                    if (row is not null && !string.IsNullOrWhiteSpace(row.FolderPath) && Directory.Exists(row.FolderPath))
                    {
                        SetActiveJob(row.ClientCode, row.FolderPath, row.Task);
                        RefreshFileTilesForCurrentContext(row.FolderPath);
                        e.Handled = true;
                        return;
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }

                SetActiveJob(null, null, null);
                try
                {
                    _vm.ReplaceFileTiles(Array.Empty<FileTileItem>());
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
                }
                e.Handled = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Jobs", ex_safe_log);
            }
        }
    }
}
