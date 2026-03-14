using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private bool _isIdleBreakActive;

        private bool IsIdleBreakContext()
        {
            try
            {
                var selectedCount = _vm.SelectedFiles?.Count ?? 0;
                if (selectedCount > 0)
                {
                    return false;
                }

                var effectiveClient = _vm.HasSelectionMetaLock
                    ? (_vm.SelectionLockedClientCode ?? string.Empty)
                    : (_vm.ActiveJobClientCode ?? string.Empty);

                if (!string.IsNullOrWhiteSpace(effectiveClient))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_vm.ActiveJobFolderPath))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

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
                    if (_cachedPrimaryBrush   != null) StartButton.Background = _cachedPrimaryBrush;
                    if (_cachedTextBlackBrush != null) StartButton.Foreground = _cachedTextBlackBrush;
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

                if (_isIdleBreakActive)
                {
                    try
                    {
                        _vm.IsPaused = false;
                        TrackerEndPause();
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ManualResumeFromBreak.IdleEndPause", ex_safe_log);
                    }

                    _isIdleBreakActive = false;
                    ResetActionButtons();
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

                _isIdleBreakActive = false;

                ClearBreakReason();

                try
                {
                    if (_cachedWarningBrush   != null) StartButton.Background = _cachedWarningBrush;
                    if (_cachedTextWhiteBrush != null) StartButton.Foreground = _cachedTextWhiteBrush;
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
                    _isIdleBreakActive = IsIdleBreakContext();
                    _vm.IsStarted = true;
                    _vm.IsPaused = true;

                    PauseWorkTimer();

                    TrackerStartSession();
                    TrackerBeginPause();
                    
                    ApplyPausedUiState();

                    return;
                }

                if (_vm.IsPaused)
                {
                    return;
                }

                _vm.IsPaused = true;
                _isIdleBreakActive = false;

                ApplyPausedUiState();

                PauseWorkTimer();

                TrackerBeginPause();

                try
                {
                    // Flush the active work delta and apply pause status in ONE request.
                    var elapsed = GetWorkTimerElapsedSeconds();
                    QueueWorkDeltaAcrossActiveFiles(
                        totalElapsedSeconds: elapsed,
                        filesToExclude: null,
                        activeSnapshotFilePaths: null, // this will use all currently active files
                        forceEvenIfPaused: true,
                        overrideStatus: "pause",
                        alwaysSend: true);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Break.FlushDeltaOnPause", ex_safe_log);
                }
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
                _isIdleBreakActive = false;
                ClearBreakReason();

                ResetWorkTimer();

                StartButton.Visibility = Visibility.Visible;
                StartButton.IsEnabled = true;
                _vm.StartButtonText = "Start";

                try
                {
                    if (_cachedPrimaryBrush   != null) StartButton.Background = _cachedPrimaryBrush;
                    if (_cachedTextBlackBrush != null) StartButton.Foreground = _cachedTextBlackBrush;
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
