using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2.Models
{
    /// <summary>
    /// Tracks per-session pause and timing state for the ExplorerV2 work session.
    /// Reset when a new session starts (user clicks Start with new files).
    /// </summary>
    public sealed class ExplorerWorkSession
    {
        public sealed class PauseReasonEntry
        {
            public string Reason { get; set; } = string.Empty;
            public double DurationSeconds { get; set; }
        }

        public DateTime? StartedAt { get; set; }
        public int PauseCount { get; set; }
        public double PauseTimeSeconds { get; set; }
        public string? CurrentPauseReason { get; set; }

        public bool IsPauseActive => _pauseStopwatch?.IsRunning ?? false;

        public double TotalPauseTimeSeconds => PauseTimeSeconds + (_pauseStopwatch?.Elapsed.TotalSeconds ?? 0);

        // Using Stopwatch instead of DateTime.Now so that clock adjustments
        // (DST transitions, NTP syncs) cannot distort pause-duration measurements.
        private Stopwatch? _pauseStopwatch;

        /// <summary>
        /// Append-only pause history (one entry per pause).
        /// </summary>
        public List<PauseReasonEntry> PauseReasonHistory { get; } = new();

        public void Reset()
        {
            StartedAt = null;
            PauseCount = 0;
            PauseTimeSeconds = 0;
            CurrentPauseReason = null;
            _pauseStopwatch?.Stop();
            _pauseStopwatch = null;
            PauseReasonHistory.Clear();
        }

        /// <summary>
        /// Begin a pause. Call when user clicks Break.
        /// </summary>
        public void BeginPause(string? reason)
        {
            PauseCount++;
            _pauseStopwatch = Stopwatch.StartNew();
            CurrentPauseReason = reason;
        }

        /// <summary>
        /// End a pause. Call when user clicks Resume / Start again.
        /// Returns the duration in seconds of this pause.
        /// </summary>
        public double EndPause()
        {
            if (_pauseStopwatch is null)
                return 0;

            _pauseStopwatch.Stop();
            var duration = _pauseStopwatch.Elapsed.TotalSeconds;
            _pauseStopwatch = null;
            PauseTimeSeconds += duration;

            if (!string.IsNullOrWhiteSpace(CurrentPauseReason))
            {
                PauseReasonHistory.Add(new PauseReasonEntry
                {
                    Reason = CurrentPauseReason,
                    DurationSeconds = duration
                });
            }

            CurrentPauseReason = null;

            return duration;
        }
    }
}
