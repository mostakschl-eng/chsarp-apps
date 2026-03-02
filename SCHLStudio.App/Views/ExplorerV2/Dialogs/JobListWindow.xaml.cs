using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.ViewModels.Windows;
using SCHLStudio.App.Views.ExplorerV2.Models;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class JobListWindow : Window
    {
        private readonly ObservableCollection<JobListRow> _rows = new();
        private readonly List<JobListRow> _allRows = new();

        private bool _suppressFilterEvents;

        private const string StatusRunningToken = "Running";
        private const string StatusQcToken = "QC";
        private const string StatusTestToken = "Test";
        private const string StatusCorrectionToken = "Correction";

        public event Action<JobListRow>? AddRequested;
        public event Action<JobListRow>? RemoveRequested;
        public event Action? ReloadRequested;

        private static void LogSuppressedError(Exception ex)
        {
            NonCriticalLog.EnqueueError("ExplorerV2", "JobListWindow.xaml", ex);
        }

        private static string NormalizePath(string? path)
        {
            try
            {
                return (path ?? string.Empty).Trim().Replace('/', '\\').TrimEnd('\\');
            }
            catch
            {
                return (path ?? string.Empty).Trim();
            }
        }

        public JobListWindow(IEnumerable<JobListRow>? rows = null)
        {
            InitializeComponent();

            try
            {
                if (rows is not null)
                {
                    foreach (var r in rows)
                    {
                        _allRows.Add(r);
                    }
                }

                JobsList.ItemsSource = _rows;
                InitializeFilters();
                ApplyRoleRestrictions();
                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void QcStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_suppressFilterEvents) return;

                try
                {
                    _suppressFilterEvents = true;
                    if (RunningStatusButton is not null) RunningStatusButton.IsChecked = false;
                    if (QcStatusButton is not null) QcStatusButton.IsChecked = true;
                    if (TestStatusButton is not null) TestStatusButton.IsChecked = false;
                    if (CorrectionStatusButton is not null) CorrectionStatusButton.IsChecked = false;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
                finally
                {
                    _suppressFilterEvents = false;
                }

                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void TestStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_suppressFilterEvents) return;

                try
                {
                    _suppressFilterEvents = true;
                    if (RunningStatusButton is not null) RunningStatusButton.IsChecked = false;
                    if (QcStatusButton is not null) QcStatusButton.IsChecked = false;
                    if (TestStatusButton is not null) TestStatusButton.IsChecked = true;
                    if (CorrectionStatusButton is not null) CorrectionStatusButton.IsChecked = false;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
                finally
                {
                    _suppressFilterEvents = false;
                }

                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void ApplyRoleRestrictions()
        {
            try
            {
                var role = ((System.Windows.Application.Current?.MainWindow?.DataContext as AppShellContext)?.CurrentRole ?? string.Empty).Trim();
                var isEmployee = string.Equals(role, "employee", StringComparison.OrdinalIgnoreCase);

                if (!isEmployee)
                {
                    return;
                }

                if (CorrectionStatusButton is not null)
                {
                    CorrectionStatusButton.IsChecked = false;
                    CorrectionStatusButton.Visibility = Visibility.Collapsed;
                }

                if (TestStatusButton is not null)
                {
                    TestStatusButton.IsChecked = false;
                    TestStatusButton.Visibility = Visibility.Collapsed;
                }

                if (QcStatusButton is not null)
                {
                    QcStatusButton.IsChecked = false;
                    QcStatusButton.Visibility = Visibility.Collapsed;
                }

                if (RunningStatusButton is not null)
                {
                    RunningStatusButton.IsChecked = true;
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private static JobListRow? FindBestAutoSelectMatch(List<JobListRow> filtered, string search)
        {
            try
            {
                if (filtered is null || filtered.Count == 0) return null;
                if (string.IsNullOrWhiteSpace(search)) return null;

                var normalizedSearchPath = NormalizePath(search);
                var isPathLike = search.Contains("\\")
                                 || search.Contains("/")
                                 || search.Contains(":\\")
                                 || search.Contains(":/")
                                 || search.StartsWith("\\\\", StringComparison.Ordinal)
                                 || search.StartsWith("//", StringComparison.Ordinal);

                if (isPathLike)
                {
                    var best = filtered
                        .Where(r => !string.IsNullOrWhiteSpace(r?.FolderPath))
                        .Select(r => new
                        {
                            Row = r,
                            Folder = NormalizePath(r!.FolderPath)
                        })
                        .Where(x => normalizedSearchPath.StartsWith(x.Folder, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(x => x.Folder.Length)
                        .FirstOrDefault();

                    if (best is not null)
                    {
                        return best.Row;
                    }
                }

                var containsMatch = filtered.FirstOrDefault(r =>
                    (!string.IsNullOrWhiteSpace(r?.ClientCode) && r.ClientCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(r?.FolderPath) && r.FolderPath.Contains(search, StringComparison.OrdinalIgnoreCase))
                );
                return containsMatch;
            }
            catch
            {
                return null;
            }
        }

        private static bool ShouldAutoActivate(JobListRow row, string search)
        {
            try
            {
                if (row is null) return false;
                if (string.IsNullOrWhiteSpace(search)) return false;

                if (!string.IsNullOrWhiteSpace(row.ClientCode)
                    && string.Equals(row.ClientCode.Trim(), search.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var normalizedSearchPath = NormalizePath(search);
                if (!string.IsNullOrWhiteSpace(row.FolderPath))
                {
                    var folder = NormalizePath(row.FolderPath);
                    if (!string.IsNullOrWhiteSpace(folder)
                        && (string.Equals(normalizedSearchPath, folder, StringComparison.OrdinalIgnoreCase)
                            || normalizedSearchPath.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void InitializeFilters()
        {
            try
            {
                _suppressFilterEvents = true;

                try
                {
                    if (RunningStatusButton is not null) RunningStatusButton.IsChecked = true;
                    if (QcStatusButton is not null) QcStatusButton.IsChecked = false;
                    if (TestStatusButton is not null) TestStatusButton.IsChecked = false;
                    if (CorrectionStatusButton is not null) CorrectionStatusButton.IsChecked = false;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
            finally
            {
                _suppressFilterEvents = false;
            }
        }

        private string GetSelectedStatusToken()
        {
            try
            {
                if (CorrectionStatusButton?.IsChecked == true)
                {
                    return StatusCorrectionToken;
                }

                if (QcStatusButton?.IsChecked == true)
                {
                    return StatusQcToken;
                }

                if (TestStatusButton?.IsChecked == true)
                {
                    return StatusTestToken;
                }

                return StatusRunningToken;
            }
            catch
            {
                return StatusRunningToken;
            }
        }

        private void ApplyFilters()
        {
            try
            {
                var selectedStatus = GetSelectedStatusToken();
                var search = (SearchTextBox?.Text ?? string.Empty).Trim();
                var normalizedSearchPath = NormalizePath(search);
                var isPathLike = !string.IsNullOrWhiteSpace(search)
                                 && (search.Contains("\\")
                                     || search.Contains("/")
                                     || search.Contains(":\\")
                                     || search.Contains(":/")
                                     || search.StartsWith("\\\\", StringComparison.Ordinal)
                                     || search.StartsWith("//", StringComparison.Ordinal));

                IEnumerable<JobListRow> query = _allRows;

                if (string.Equals(selectedStatus, StatusRunningToken, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals((r?.Status ?? string.Empty).Trim(), "running", StringComparison.OrdinalIgnoreCase)
                        && string.Equals((r?.Type ?? string.Empty).Trim(), "general", StringComparison.OrdinalIgnoreCase));
                }
                else if (string.Equals(selectedStatus, StatusQcToken, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals((r?.Status ?? string.Empty).Trim(), "running", StringComparison.OrdinalIgnoreCase)
                        && ((r?.Type ?? string.Empty).Trim().StartsWith("qc", StringComparison.OrdinalIgnoreCase)));
                }
                else if (string.Equals(selectedStatus, StatusTestToken, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals((r?.Status ?? string.Empty).Trim(), "running", StringComparison.OrdinalIgnoreCase)
                        && (
                            string.Equals((r?.Type ?? string.Empty).Trim(), "istest", StringComparison.OrdinalIgnoreCase)
                            || string.Equals((r?.Type ?? string.Empty).Trim(), "test", StringComparison.OrdinalIgnoreCase)
                        ));
                }
                else if (string.Equals(selectedStatus, StatusCorrectionToken, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r => string.Equals((r?.Status ?? string.Empty).Trim(), "correction", StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(r =>
                        (!string.IsNullOrWhiteSpace(r?.ClientCode) && r.ClientCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(r?.FolderPath)
                            && ((isPathLike && NormalizePath(r.FolderPath).Length > 0
                                    && (normalizedSearchPath.StartsWith(NormalizePath(r.FolderPath), StringComparison.OrdinalIgnoreCase)
                                        || NormalizePath(r.FolderPath).Contains(normalizedSearchPath, StringComparison.OrdinalIgnoreCase)))
                                || (!isPathLike && r.FolderPath.Contains(search, StringComparison.OrdinalIgnoreCase))))
                    );
                }

                var filtered = query.ToList();

                _rows.Clear();
                foreach (var r in filtered)
                {
                    _rows.Add(r);
                }

                RefreshRowNumbers();
                RefreshCounts();

                try
                {
                    if (JobsList is not null)
                    {
                        if (string.IsNullOrWhiteSpace(search) || filtered.Count == 0)
                        {
                            JobsList.SelectedItem = null;
                        }
                        else
                        {
                            var best = FindBestAutoSelectMatch(filtered, search);
                            if (best is not null)
                            {
                                JobsList.SelectedItem = best;
                                JobsList.ScrollIntoView(best);
                            }
                            else
                            {
                                JobsList.SelectedItem = null;
                            }
                        }
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }

                try
                {
                    if (JobsList is not null)
                    {
                        JobsList.Items.Refresh();
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void RefreshCounts()
        {
            try
            {
                var total = _rows.Count;
                var active = 0;
                try
                {
                    active = _rows.Count(x => x is not null && x.IsAdded);
                }
                catch
                {
                    active = 0;
                }

                if (CountsText is not null)
                {
                    CountsText.Text = $"Total: {total}   Active: {active}";
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        public void ReplaceRows(IEnumerable<JobListRow>? rows)
        {
            try
            {
                _allRows.Clear();
                if (rows is not null)
                {
                    foreach (var r in rows)
                    {
                        _allRows.Add(r);
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void RefreshRowNumbers()
        {
            try
            {
                for (var i = 0; i < _rows.Count; i++)
                {
                    _rows[i].RowNumber = i + 1;
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (_suppressFilterEvents) return;
                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void RunningStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_suppressFilterEvents) return;

                try
                {
                    _suppressFilterEvents = true;
                    if (RunningStatusButton is not null) RunningStatusButton.IsChecked = true;
                    if (QcStatusButton is not null) QcStatusButton.IsChecked = false;
                    if (TestStatusButton is not null) TestStatusButton.IsChecked = false;
                    if (CorrectionStatusButton is not null) CorrectionStatusButton.IsChecked = false;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
                finally
                {
                    _suppressFilterEvents = false;
                }

                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void CorrectionStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_suppressFilterEvents) return;

                try
                {
                    _suppressFilterEvents = true;
                    if (RunningStatusButton is not null) RunningStatusButton.IsChecked = false;
                    if (QcStatusButton is not null) QcStatusButton.IsChecked = false;
                    if (TestStatusButton is not null) TestStatusButton.IsChecked = false;
                    if (CorrectionStatusButton is not null) CorrectionStatusButton.IsChecked = true;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
                finally
                {
                    _suppressFilterEvents = false;
                }

                ApplyFilters();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void ToggleRowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe)
                {
                    return;
                }

                if (fe.Tag is not JobListRow row)
                {
                    return;
                }

                if (!row.IsAdded)
                {
                    try
                    {
                        var anyOtherActive = _rows.Any(x => !ReferenceEquals(x, row) && x.IsAdded);
                        if (anyOtherActive)
                        {
                            try
                            {
                                System.Windows.MessageBox.Show(
                                    "Only one client can be active. Deactivate the current active job first.",
                                    "SCHL App",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            catch (Exception ex_safe_log)
                            {
                                LogSuppressedError(ex_safe_log);
                            }

                            return;
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError(ex_safe_log);
                    }

                    AddRequested?.Invoke(row);
                    row.IsAdded = true;
                }
                else
                {
                    RemoveRequested?.Invoke(row);
                    row.IsAdded = false;
                }

                try
                {
                    JobsList.Items.Refresh();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReloadRequested?.Invoke();

                try
                {
                    JobsList.Items.Refresh();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError(ex_safe_log);
                }

                RefreshCounts();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void CopyFolderPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe)
                {
                    return;
                }

                if (fe.Tag is not JobListRow row)
                {
                    return;
                }

                var text = row.FolderPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                System.Windows.Clipboard.SetText(text);
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void ClientCodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount < 2)
                {
                    return;
                }

                if (sender is not FrameworkElement fe)
                {
                    return;
                }

                if (fe.DataContext is not JobListRow row)
                {
                    return;
                }

                var text = row.ClientCode ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                System.Windows.Clipboard.SetText(text);
                e.Handled = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void FolderPathText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount < 2)
                {
                    return;
                }

                if (sender is not FrameworkElement fe)
                {
                    return;
                }

                if (fe.DataContext is not JobListRow row)
                {
                    return;
                }

                var text = row.FolderPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                System.Windows.Clipboard.SetText(text);
                e.Handled = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }

        private void WindowRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError(ex_safe_log);
            }
        }
    }
}
