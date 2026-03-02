using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using SCHLStudio.App.Views.ExplorerV2.Dialogs;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private const int IdleThresholdSeconds = 10;
        private const string AutoPauseReason = "auto pause";

        private readonly DispatcherTimer _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private IdleWarningWindow? _idleWarning;
        private bool _isIdleAutoPaused;

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private static int GetIdleSeconds()
        {
            try
            {
                var lii = new LASTINPUTINFO();
                lii.cbSize = (uint)Marshal.SizeOf(lii);
                if (!GetLastInputInfo(ref lii))
                {
                    return 0;
                }

                var idleMs = unchecked((int)Environment.TickCount - (int)lii.dwTime);
                if (idleMs < 0)
                {
                    idleMs = 0;
                }

                return idleMs / 1000;
            }
            catch
            {
                return 0;
            }
        }

        private void StartIdleMonitor()
        {
            try
            {
                if (_idleTimer.IsEnabled)
                {
                    return;
                }

                _idleTimer.Tick -= IdleTimer_Tick;
                _idleTimer.Tick += IdleTimer_Tick;
                _idleTimer.Start();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }
        }

        private void StopIdleMonitor()
        {
            try
            {
                _idleTimer.Stop();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }

            try
            {
                HideIdleWarning();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }

            _isIdleAutoPaused = false;
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (!_vm.IsStarted)
                {
                    StopIdleMonitor();
                    return;
                }

                var idleSeconds = GetIdleSeconds();

                // Only auto-pause when currently running (not paused)
                if (!_vm.IsPaused)
                {
                    if (!_isIdleAutoPaused && idleSeconds >= IdleThresholdSeconds)
                    {
                        AutoPauseForIdle();
                    }

                    return;
                }

                // When paused due to idle, resume as soon as user becomes active again.
                if (_isIdleAutoPaused && idleSeconds < 1)
                {
                    AutoResumeFromIdle();
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }
        }

        private void ShowIdleWarning()
        {
            try
            {
                if (_idleWarning is not null)
                {
                    if (!_idleWarning.IsVisible)
                    {
                        _idleWarning.Show();
                    }

                    return;
                }

                _idleWarning = new IdleWarningWindow();

                try
                {
                    var owner = Window.GetWindow(this);
                    if (owner is not null)
                    {
                        _idleWarning.Owner = owner;
                    }
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
                }

                _idleWarning.Show();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }
        }

        private void HideIdleWarning()
        {
            try
            {
                if (_idleWarning is null)
                {
                    return;
                }

                try
                {
                    _idleWarning.Close();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
                }

                _idleWarning = null;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }
        }

        private void AutoPauseForIdle()
        {
            try
            {
                // Ensure pause reason is captured in sync payload
                _vm.SelectedBreakReason = AutoPauseReason;

                EnsurePausedState();
                _isIdleAutoPaused = true;

                ShowIdleWarning();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }
        }

        private void AutoResumeFromIdle()
        {
            try
            {
                if (!_isIdleAutoPaused)
                {
                    return;
                }

                HideIdleWarning();

                if (!_vm.IsStarted || !_vm.IsPaused)
                {
                    _isIdleAutoPaused = false;
                    return;
                }

                // Resume using the same code path as StartButton_Click resume branch
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
                    LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
                }

                _isIdleAutoPaused = false;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Idle", ex_safe_log);
            }
        }
    }
}
