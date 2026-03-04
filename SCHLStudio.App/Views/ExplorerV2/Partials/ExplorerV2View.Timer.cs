using System;
using System.Windows.Threading;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private readonly DispatcherTimer _workTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private DateTime? _workTimerRunningSince;
        private TimeSpan _workTimerElapsed;

        private void EnsureTimerHooked()
        {
            try
            {
                _workTimer.Tick -= WorkTimer_Tick;
                _workTimer.Tick += WorkTimer_Tick;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureTimerHooked error: {ex.Message}");
            }
        }

        private void WorkTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                UpdateTimerText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkTimer_Tick error: {ex.Message}");
            }
        }

        private void StartWorkTimerFresh()
        {
            try
            {
                EnsureTimerHooked();
                _workTimerElapsed = TimeSpan.Zero;
                _workTimerRunningSince = DateTime.Now;
                _workTimer.Start();
                UpdateTimerText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartWorkTimerFresh error: {ex.Message}");
            }
        }

        private void ResumeWorkTimer()
        {
            try
            {
                EnsureTimerHooked();
                if (_workTimerRunningSince is not null)
                {
                    return;
                }

                _workTimerRunningSince = DateTime.Now;
                _workTimer.Start();
                UpdateTimerText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResumeWorkTimer error: {ex.Message}");
            }
        }

        private void PauseWorkTimer()
        {
            try
            {
                if (_workTimerRunningSince is null)
                {
                    _workTimer.Stop();
                    return;
                }

                _workTimerElapsed += (DateTime.Now - _workTimerRunningSince.Value);
                _workTimerRunningSince = null;
                _workTimer.Stop();
                UpdateTimerText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PauseWorkTimer error: {ex.Message}");
            }
        }

        private void ResetWorkTimer()
        {
            try
            {
                _workTimer.Stop();
                _workTimerRunningSince = null;
                _workTimerElapsed = TimeSpan.Zero;
                UpdateTimerText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetWorkTimer error: {ex.Message}");
            }
        }

        private void UpdateTimerText()
        {
            try
            {
                var total = _workTimerElapsed;
                if (_workTimerRunningSince is not null)
                {
                    total += (DateTime.Now - _workTimerRunningSince.Value);
                }

                try
                {
                    _vm.TimerDisplay = total.ToString(@"hh\:mm\:ss");
                }
                catch (Exception ex)
                {
                    LogSuppressedError("UpdateTimerText.TimerDisplay", ex);
                }

                try
                {
                    UpdateTimerBorder(total);
                }
                catch (Exception ex)
                {
                    LogSuppressedError("UpdateTimerText.UpdateTimerBorder", ex);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTimerText outer error: {ex.Message}");
            }
        }

        private void UpdateTimerBorder(TimeSpan elapsed)
        {
            try
            {
                if (TimerBorder is null)
                {
                    return;
                }

                if (!_vm.IsStarted || _vm.IsPaused)
                {
                    if (_cachedDividerBrush != null) TimerBorder.BorderBrush = _cachedDividerBrush;
                    return;
                }

                var selectedCount = _vm.SelectedFiles?.Count ?? 0;
                if (selectedCount <= 0)
                {
                    if (_cachedDividerBrush != null) TimerBorder.BorderBrush = _cachedDividerBrush;
                    return;
                }

                var etMinutes = _cachedActiveJobEtMinutes;
                if (etMinutes <= 0)
                {
                    if (_cachedDividerBrush != null) TimerBorder.BorderBrush = _cachedDividerBrush;
                    return;
                }

                var limit = TimeSpan.FromMinutes(etMinutes);
                var perFileElapsed = selectedCount > 0
                    ? TimeSpan.FromSeconds(elapsed.TotalSeconds / selectedCount)
                    : elapsed;

                var ok = perFileElapsed <= limit;
                var brush = ok ? _cachedPrimaryBrush : _cachedDangerBrush;
                TimerBorder.BorderBrush = brush
                    ?? (ok ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTimerBorder error: {ex.Message}");
            }
        }

    }
}
