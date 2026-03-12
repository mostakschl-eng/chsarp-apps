using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.ViewModels.Windows;
using SCHLStudio.App.Views.ExplorerV2.Models;

namespace SCHLStudio.App.Views.ExplorerV2
{
    /// <summary>
    /// Inline Job List panel logic — mirrors the behaviour of the former
    /// Inline Job List panel rendered inside the ExplorerV2 content grid.
    /// </summary>
    public partial class ExplorerV2View
    {
        // ── state ──────────────────────────────────────────────────────
        private readonly ObservableCollection<JobListRow> _jlpRows = new();
        private bool _jlpSuppressFilter;
        private bool _isJobListPanelOpen;

        private const string JlpStatusRunning = "Running";
        private const string JlpStatusQc = "QC";
        private const string JlpStatusTest = "Test";
        private const string JlpStatusCorrection = "Correction";

        // ── toggle ─────────────────────────────────────────────────────
        private void ToggleJobListPanel()
        {
            try
            {
                if (_isJobListPanelOpen)
                {
                    CloseJobListPanel();
                }
                else
                {
                    OpenJobListPanel();
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void OpenJobListPanel()
        {
            try
            {
                _isJobListPanelOpen = true;

                if (JobListDividerColumn is not null)
                    JobListDividerColumn.Width = new GridLength(10);

                if (JobListPanelColumn is not null)
                    JobListPanelColumn.Width = new GridLength(1.5, GridUnitType.Star);

                if (JobListDivider is not null)
                    JobListDivider.Visibility = Visibility.Visible;

                if (JobListPanel is not null)
                    JobListPanel.Visibility = Visibility.Visible;

                if (JlpJobsList is not null && JlpJobsList.ItemsSource is null)
                    JlpJobsList.ItemsSource = _jlpRows;

                InitializeJobListPanelFilters();
                JlpApplyRoleRestrictions();
                JlpSyncRows();
                JlpApplyFilters();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void CloseJobListPanel()
        {
            try
            {
                _isJobListPanelOpen = false;

                if (JobListDividerColumn is not null)
                    JobListDividerColumn.Width = new GridLength(0);

                if (JobListPanelColumn is not null)
                    JobListPanelColumn.Width = new GridLength(0);

                if (JobListDivider is not null)
                    JobListDivider.Visibility = Visibility.Collapsed;

                if (JobListPanel is not null)
                    JobListPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        // ── initialise ────────────────────────────────────────────────
        private void InitializeJobListPanelFilters()
        {
            try
            {
                _jlpSuppressFilter = true;
                try
                {
                    if (JlpRunningButton is not null) JlpRunningButton.IsChecked = true;
                    if (JlpQcButton is not null) JlpQcButton.IsChecked = false;
                    if (JlpCorrectionButton is not null) JlpCorrectionButton.IsChecked = false;
                    if (JlpTestButton is not null) JlpTestButton.IsChecked = false;
                }
                catch (Exception ex)
                {
                    LogSuppressedError("ExplorerV2View.JobListPanel", ex);
                }
            }
            finally
            {
                _jlpSuppressFilter = false;
            }
        }

        private void JlpApplyRoleRestrictions()
        {
            try
            {
                var role = ((System.Windows.Application.Current?.MainWindow?.DataContext as AppShellContext)?.CurrentRole ?? string.Empty).Trim();
                var isEmployee = string.Equals(role, "employee", StringComparison.OrdinalIgnoreCase);
                if (!isEmployee) return;

                if (JlpCorrectionButton is not null)
                {
                    JlpCorrectionButton.IsChecked = false;
                    JlpCorrectionButton.Visibility = Visibility.Collapsed;
                }
                if (JlpTestButton is not null)
                {
                    JlpTestButton.IsChecked = false;
                    JlpTestButton.Visibility = Visibility.Collapsed;
                }
                if (JlpQcButton is not null)
                {
                    JlpQcButton.IsChecked = false;
                    JlpQcButton.Visibility = Visibility.Collapsed;
                }
                if (JlpRunningButton is not null)
                {
                    JlpRunningButton.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        /// <summary>Copy the shared <c>_jobListRows</c> into the panel's observable collection.</summary>
        private void JlpSyncRows()
        {
            try
            {
                // Keep the panel in-sync with the latest API rows.
                JlpApplyFilters();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        /// <summary>Refresh visible rows after external data change (e.g. API reload).</summary>
        internal void JlpRefreshAfterDataChange()
        {
            try
            {
                if (!_isJobListPanelOpen) return;
                JlpApplyFilters();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        // ── filter logic ──────────────────────────────────────────────
        private string JlpGetSelectedStatus()
        {
            try
            {
                if (JlpCorrectionButton?.IsChecked == true) return JlpStatusCorrection;
                if (JlpQcButton?.IsChecked == true) return JlpStatusQc;
                if (JlpTestButton?.IsChecked == true) return JlpStatusTest;
                return JlpStatusRunning;
            }
            catch { return JlpStatusRunning; }
        }

        private static string JlpNormalizePath(string? path)
        {
            try { return (path ?? string.Empty).Trim().Replace('/', '\\').TrimEnd('\\'); }
            catch { return (path ?? string.Empty).Trim(); }
        }

        private void JlpApplyFilters()
        {
            try
            {
                var status = JlpGetSelectedStatus();
                var search = (JlpSearchTextBox?.Text ?? string.Empty).Trim();
                var normalizedSearch = JlpNormalizePath(search);
                var isPathLike = !string.IsNullOrWhiteSpace(search)
                    && (search.Contains('\\') || search.Contains('/') || search.Contains(":\\") || search.Contains(":/")
                        || search.StartsWith("\\\\", StringComparison.Ordinal) || search.StartsWith("//", StringComparison.Ordinal));

                IEnumerable<JobListRow> query = _jobListRows; // shared backing list

                if (string.Equals(status, JlpStatusRunning, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals((r?.Status ?? "").Trim(), "running", StringComparison.OrdinalIgnoreCase)
                        && string.Equals((r?.Type ?? "").Trim(), "general", StringComparison.OrdinalIgnoreCase));
                }
                else if (string.Equals(status, JlpStatusQc, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals((r?.Status ?? "").Trim(), "running", StringComparison.OrdinalIgnoreCase)
                        && (r?.Type ?? "").Trim().StartsWith("qc", StringComparison.OrdinalIgnoreCase));
                }
                else if (string.Equals(status, JlpStatusTest, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r =>
                        string.Equals((r?.Status ?? "").Trim(), "running", StringComparison.OrdinalIgnoreCase)
                        && (string.Equals((r?.Type ?? "").Trim(), "istest", StringComparison.OrdinalIgnoreCase)
                            || string.Equals((r?.Type ?? "").Trim(), "test", StringComparison.OrdinalIgnoreCase)));
                }
                else if (string.Equals(status, JlpStatusCorrection, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r => string.Equals((r?.Status ?? "").Trim(), "correction", StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(r =>
                    {
                        if (!string.IsNullOrWhiteSpace(r?.ClientCode) && r.ClientCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                            return true;

                        if (string.IsNullOrWhiteSpace(r?.FolderPath))
                            return false;

                        if (isPathLike)
                        {
                            var normalizedFolder = JlpNormalizePath(r.FolderPath);
                            if (normalizedFolder.Length > 0
                                && (normalizedSearch.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase)
                                    || normalizedFolder.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
                                return true;
                        }
                        else if (r.FolderPath.Contains(search, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        return false;
                    });
                }

                var filtered = query.ToList();

                // Batch update: detach ItemsSource, swap contents, re-attach.
                // This fires a single Reset instead of N individual Add notifications.
                if (JlpJobsList is not null)
                    JlpJobsList.ItemsSource = null;

                _jlpRows.Clear();
                for (var i = 0; i < filtered.Count; i++)
                {
                    filtered[i].RowNumber = i + 1;
                    _jlpRows.Add(filtered[i]);
                }

                if (JlpJobsList is not null)
                    JlpJobsList.ItemsSource = _jlpRows;

                JlpRefreshCounts();
                JlpAutoSelect(filtered, search);
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }


        private void JlpAutoSelect(List<JobListRow> filtered, string search)
        {
            try
            {
                if (JlpJobsList is null) return;

                if (string.IsNullOrWhiteSpace(search) || filtered.Count == 0)
                {
                    JlpJobsList.SelectedItem = null;
                    return;
                }

                var best = JlpFindBestMatch(filtered, search);
                if (best is not null)
                {
                    JlpJobsList.SelectedItem = best;
                    JlpJobsList.ScrollIntoView(best);
                }
                else
                {
                    JlpJobsList.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private static JobListRow? JlpFindBestMatch(List<JobListRow> filtered, string search)
        {
            try
            {
                if (filtered is null || filtered.Count == 0 || string.IsNullOrWhiteSpace(search)) return null;

                var normalizedSearch = JlpNormalizePath(search);
                var isPathLike = search.Contains('\\') || search.Contains('/') || search.Contains(":\\") || search.Contains(":/")
                    || search.StartsWith("\\\\", StringComparison.Ordinal) || search.StartsWith("//", StringComparison.Ordinal);

                if (isPathLike)
                {
                    var best = filtered
                        .Where(r => !string.IsNullOrWhiteSpace(r?.FolderPath))
                        .Select(r => new { Row = r, Folder = JlpNormalizePath(r!.FolderPath) })
                        .Where(x => normalizedSearch.StartsWith(x.Folder, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(x => x.Folder.Length)
                        .FirstOrDefault();
                    if (best is not null) return best.Row;
                }

                return filtered.FirstOrDefault(r =>
                    (!string.IsNullOrWhiteSpace(r?.ClientCode) && r.ClientCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(r?.FolderPath) && r.FolderPath.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }
            catch { return null; }
        }

        private void JlpRefreshCounts()
        {
            try
            {
                if (JlpCountsText is null) return;
                var total = _jlpRows.Count;
                var active = 0;
                try { active = _jlpRows.Count(x => x is not null && x.IsAdded); } catch { }
                JlpCountsText.Text = $"Total: {total}   Active: {active}";
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        // ── event handlers (wired from XAML) ──────────────────────────
        private void JlpCloseButton_Click(object sender, RoutedEventArgs e)
        {
            try { CloseJobListPanel(); }
            catch (Exception ex) { LogSuppressedError("ExplorerV2View.JobListPanel", ex); }
        }

        private void JlpReloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = LoadJobListFromApiAsync();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_jlpSuppressFilter) return;

                // Auto-open the panel when the user types a search query while it is closed
                if (!_isJobListPanelOpen && !string.IsNullOrWhiteSpace(JlpSearchTextBox?.Text))
                {
                    OpenJobListPanel();
                }

                JlpApplyFilters();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_jlpSuppressFilter) return;

                var clicked = sender as ToggleButton;
                try
                {
                    _jlpSuppressFilter = true;
                    if (JlpRunningButton is not null) JlpRunningButton.IsChecked = ReferenceEquals(clicked, JlpRunningButton);
                    if (JlpQcButton is not null) JlpQcButton.IsChecked = ReferenceEquals(clicked, JlpQcButton);
                    if (JlpCorrectionButton is not null) JlpCorrectionButton.IsChecked = ReferenceEquals(clicked, JlpCorrectionButton);
                    if (JlpTestButton is not null) JlpTestButton.IsChecked = ReferenceEquals(clicked, JlpTestButton);
                }
                finally
                {
                    _jlpSuppressFilter = false;
                }

                JlpApplyFilters();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpToggleRowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe) return;
                if (fe.Tag is not JobListRow row) return;

                if (!row.IsAdded)
                {
                    // Only one active job at a time across all tabs
                    var anyOtherActive = _jobListRows.Any(x => !ReferenceEquals(x, row) && x.IsAdded);
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
                        catch (Exception ex) { LogSuppressedError("ExplorerV2View.JobListPanel", ex); }
                        return;
                    }

                    // Activate
                    row.IsAdded = true;
                    JlpOnAddRequested(row);
                }
                else
                {
                    // Deactivate
                    row.IsAdded = false;
                    JlpOnRemoveRequested(row);
                }

                try { JlpJobsList?.Items.Refresh(); } catch { }
                JlpRefreshCounts();
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpCopyFolderPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe) return;
                if (fe.Tag is not JobListRow row) return;
                var text = row.FolderPath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    System.Windows.Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpClientCodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount < 2) return;
                if (sender is not FrameworkElement fe) return;
                if (fe.DataContext is not JobListRow row) return;
                var text = row.ClientCode ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    System.Windows.Clipboard.SetText(text);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpFolderPathText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount < 2) return;
                if (sender is not FrameworkElement fe) return;
                if (fe.DataContext is not JobListRow row) return;
                var text = row.FolderPath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    System.Windows.Clipboard.SetText(text);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        // ── callbacks (same logic as the former dialog event delegates) ────
        private void JlpOnAddRequested(JobListRow row)
        {
            try
            {


                if (string.IsNullOrWhiteSpace(row.FolderPath)) return;
                if (!System.IO.Directory.Exists(row.FolderPath)) return;

                SetActiveJob(row.ClientCode, row.FolderPath, row.Task);
                RefreshFileTilesForCurrentContext(row.FolderPath);
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }

        private void JlpOnRemoveRequested(JobListRow _)
        {
            try
            {
                _fileIndexService.InvalidateDoneRootCache(GetActiveJobFolderPath());
                SetActiveJob(null, null, null);
                try { _vm.ReplaceFileTiles(Array.Empty<FileTileItem>()); }
                catch (Exception ex) { LogSuppressedError("ExplorerV2View.JobListPanel", ex); }
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExplorerV2View.JobListPanel", ex);
            }
        }
    }
}
