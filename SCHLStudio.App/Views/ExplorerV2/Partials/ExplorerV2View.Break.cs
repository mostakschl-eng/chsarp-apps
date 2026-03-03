using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private void ApplyPausedUiState()
        {
            try
            {
                _vm.StartButtonText = "Resume";
                _vm.IsFinishEnabled = false;
                _vm.IsWalkOutEnabled = false;
                _vm.IsSkipEnabled = false;

                try
                {
                    var bg = TryFindResource("PrimaryBrush") as System.Windows.Media.Brush;
                    var fg = TryFindResource("TextBlackBrush") as System.Windows.Media.Brush;
                    if (bg != null) StartButton.Background = bg;
                    if (fg != null) StartButton.Foreground = fg;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Break", ex_safe_log);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyPausedUiState error: {ex.Message}");
            }
        }

        private void ClearBreakReason()
        {
            try
            {
                _vm.SelectedBreakReason = null;
                _vm.BreakNote = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearBreakReason error: {ex.Message}");
            }
        }

        private static IReadOnlyList<string> GetPauseReasonsOrDefault()
        {
            var fallback = new[] { "Toilet", "Meeting", "Breakfast", "Lunch", "Dinner", "Namaz" };

            try
            {
                var cfg = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    as Microsoft.Extensions.Configuration.IConfiguration;

                var list = new List<string>();
                try
                {
                    foreach (var child in (cfg?.GetSection("PauseReasons")?.GetSection("Default")?.GetChildren()
                                 ?? Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>()))
                    {
                        var v = (child?.Value ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            list.Add(v);
                        }
                    }
                }
                catch
                {
                    list.Clear();
                }

                list = list
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return list.Count == 0 ? fallback : list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPauseReasonsOrDefault error: {ex.Message}");
                return fallback;
            }
        }

        private void ExecuteBreakWorkflowFromVm()
        {
            try
            {
                var reasons = GetPauseReasonsOrDefault();

                var owner = Window.GetWindow(this);

                Action<string, string> onBreakStarted = (r, n) =>
                {
                    _vm.SelectedBreakReason = (r ?? string.Empty).Trim();
                    _vm.BreakNote = (n ?? string.Empty).Trim();
                    EnsurePausedState();
                };

                var dlg = new BreakDialog(
                    reasons,
                    selectedReason: _vm.SelectedBreakReason,
                    note: _vm.BreakNote,
                    onBreakStarted: onBreakStarted)
                {
                    Owner = owner
                };

                var ok = dlg.ShowDialog();
                if (ok == true && _vm.IsPaused)
                {
                    ManualResumeFromBreak();
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("ExecuteBreakWorkflowFromVm", ex);
            }
        }

        private void ManualResumeFromBreak()
        {
            try
            {
                if (!_vm.IsStarted || !_vm.IsPaused)
                {
                    return;
                }

                _vm.IsPaused = false;
                _vm.StartButtonText = "Pause";

                _vm.IsFinishEnabled = true;
                _vm.IsWalkOutEnabled = true;
                _vm.IsSkipEnabled = true;

                TrackerEndPause();
                TrackerQueueResumed(GetTrackerTargetFullPaths());
                ResumeWorkTimer();

                ClearBreakReason();

                try
                {
                    var bg = TryFindResource("WarningBrush") as System.Windows.Media.Brush;
                    var fg = TryFindResource("TextWhiteBrush") as System.Windows.Media.Brush;
                    if (bg != null) StartButton.Background = bg;
                    if (fg != null) StartButton.Foreground = fg;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Break", ex_safe_log);
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("ManualResumeFromBreak", ex);
            }
        }

        private void EnsurePausedState()
        {
            try
            {
                if (!_vm.IsStarted)
                {
                    _vm.IsStarted = true;
                    _vm.IsPaused = true;

                    PauseWorkTimer();

                    TrackerStartSession();
                    TrackerBeginPause();
                    TrackerQueuePaused(GetTrackerTargetFullPaths());

                    ApplyPausedUiState();

                    return;
                }

                if (_vm.IsPaused)
                {
                    return;
                }

                _vm.IsPaused = true;

                ApplyPausedUiState();

                PauseWorkTimer();

                try
                {
                    // Flush the last active work delta immediately on pause.
                    // Heartbeat only sends once per minute, so without this the backend can lag.
                    var elapsed = GetWorkTimerElapsedSeconds();
                    QueueWorkDeltaAcrossActiveFiles(
                        totalElapsedSeconds: elapsed,
                        filesToExclude: null,
                        activeSnapshotFilePaths: null,
                        forceEvenIfPaused: true);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Break.FlushDeltaOnPause", ex_safe_log);
                }

                TrackerBeginPause();
                TrackerQueuePaused(GetTrackerTargetFullPaths());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsurePausedState error: {ex.Message}");
            }
        }

        private void ResetActionButtons()
        {
            try
            {
                try
                {
                    StopIdleMonitor();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Break", ex_safe_log);
                }

                _vm.IsStarted = false;
                _vm.IsPaused = false;
                ClearBreakReason();

                ResetWorkTimer();

                StartButton.Visibility = Visibility.Visible;
                StartButton.IsEnabled = true;
                _vm.StartButtonText = "Start";

                try
                {
                    var bg = TryFindResource("PrimaryBrush") as System.Windows.Media.Brush;
                    var fg = TryFindResource("TextBlackBrush") as System.Windows.Media.Brush;
                    if (bg != null) StartButton.Background = bg;
                    if (fg != null) StartButton.Foreground = fg;
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Break", ex_safe_log);
                }

                _vm.ResetActionState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetActionButtons error: {ex.Message}");
            }
        }
    }
}
