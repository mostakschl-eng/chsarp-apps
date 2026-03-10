using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.Services.Api.Tracker;
using SCHLStudio.App.Views.ExplorerV2.Models;

namespace SCHLStudio.App.Views.ExplorerV2
{
    /// <summary>
    /// Tracker integration — all database sync logic for ExplorerV2.
    /// Queues work log entries to be sent to POST /tracker/sync (per-file)
    /// or POST /tracker/sync-qc (QC).
    /// </summary>
    public partial class ExplorerV2View
    {
        // ── Fields ──

        private TrackerSyncService? _trackerSync;
        private readonly ExplorerWorkSession _workSession = new();

        private readonly HashSet<string> _inactiveTrackerFiles = new(StringComparer.OrdinalIgnoreCase);
        private int _lastSentWorkSeconds;

        private List<string> _activeTrackerFilePathsSnapshot = new();

        // ── Initialization ──

        /// <summary>
        /// Call once from the constructor (after DI is available).
        /// Creates the TrackerSyncService and starts the background worker.
        /// </summary>
        private void InitializeTrackerSync()
        {
            InitializeTrackerSync(null);
        }

        private void InitializeTrackerSync(string? userNameOverride)
        {
            try
            {
                if (_trackerSync is not null)
                {
                    return;
                }

                var app = System.Windows.Application.Current as SCHLStudio.App.App;
                if (app?.ServiceProvider is null) return;

                var apiClient = app.ServiceProvider.GetService(typeof(IApiClient)) as ApiClient;
                if (apiClient is null) return;

                var userName = (userNameOverride ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    userName = Configuration.AppConfig.StorageUserSegment;
                }

                _trackerSync = apiClient.EnsureTrackerSync(userName);

                Debug.WriteLine("[ExplorerV2.Tracker] Initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] Init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Call from ExplorerV2View_Unloaded to stop the sync worker.
        /// </summary>
        private void ShutdownTrackerSync()
        {
            try
            {
                _trackerSync = null;
                Debug.WriteLine("[ExplorerV2.Tracker] Shutdown");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] Shutdown error: {ex.Message}");
            }
        }

        // ── Session ──

        /// <summary>
        /// Reset session tracking (call when user clicks Start for a fresh session).
        /// </summary>
        private void TrackerStartSession()
        {
            try
            {
                _workSession.Reset();
                _workSession.StartedAt = DateTime.UtcNow;

                _inactiveTrackerFiles.Clear();
                _lastSentWorkSeconds = 0;
                _activeTrackerFilePathsSnapshot = new List<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerStartSession error: {ex.Message}");
            }
        }

        /// <summary>
        /// Record a pause start (call when user pauses).
        /// </summary>
        private void TrackerBeginPause()
        {
            try
            {
                var reason = _vm.SelectedBreakReason;
                _workSession.BeginPause(reason);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerBeginPause error: {ex.Message}");
            }
        }

        /// <summary>
        /// Record a pause end (call when user resumes).
        /// </summary>
        private void TrackerEndPause()
        {
            try
            {
                _workSession.EndPause();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerEndPause error: {ex.Message}");
            }
        }

        // ── Queueing ──

        /// <summary>
        /// Queue "working" status for each selected file (on Start).
        /// </summary>
        private void TrackerQueueWorking(IReadOnlyList<string> filePaths)
        {
            try
            {
                if (_trackerSync is null || filePaths.Count == 0) return;
                _activeTrackerFilePathsSnapshot = filePaths
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                TrackerQueueQcStatus(filePaths, "working");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerQueueWorking error: {ex.Message}");
            }
        }

        private void TryQueueWorkingTrackerHeartbeat()
        {
            try
            {
                if (_trackerSync is null)
                {
                    return;
                }

                if (!_vm.IsStarted)
                {
                    _lastSentWorkSeconds = 0;
                    return;
                }

                var allFilePaths = GetTrackerTargetFullPaths();
                if (allFilePaths.Count == 0 && _activeTrackerFilePathsSnapshot.Count > 0)
                {
                    allFilePaths = _activeTrackerFilePathsSnapshot;
                }
                if (allFilePaths.Count == 0)
                {
                    return;
                }

                // Keep the heartbeat simple: while actively working, flush deltas at most once per minute.
                // Pause/resume updates are queued by Break/Idle events.
                if (_vm.IsPaused)
                {
                    return;
                }

                var workTotalSeconds = GetWorkTimerElapsedSeconds();
                if (workTotalSeconds <= 0)
                {
                    return;
                }

                if (workTotalSeconds - _lastSentWorkSeconds < 60)
                {
                    return;
                }

                QueueWorkDeltaAcrossActiveFiles(workTotalSeconds, filesToExclude: null);
            }
            catch (Exception hbEx)
            {
                System.Diagnostics.Debug.WriteLine($"TryQueueWorkingTrackerHeartbeat error: {hbEx.Message}");
            }
        }



        /// <summary>
        /// Queue "working" status for each selected file (on Resume after pause).
        /// </summary>
        private void TrackerQueueResumed(IReadOnlyList<string> filePaths)
        {
            try
            {
                if (_trackerSync is null) return;

                // Prefer active snapshot; merge selection too.
                var merged = new List<string>();
                if (_activeTrackerFilePathsSnapshot.Count > 0)
                {
                    merged.AddRange(_activeTrackerFilePathsSnapshot);
                }
                if (filePaths != null && filePaths.Count > 0)
                {
                    merged.AddRange(filePaths);
                }

                var distinct = merged
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinct.Count == 0) return;

                _activeTrackerFilePathsSnapshot = distinct;
                TrackerQueueQcStatus(distinct, "working");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerQueueResumed error: {ex.Message}");
            }
        }

        /// <summary>
        /// Queue "done" batch for QC work types (QC 1, QC 2, QC AC).
        /// </summary>
        private void TrackerQueueDoneBatch(IReadOnlyList<string> filePaths, int totalElapsedSeconds, IReadOnlyList<string>? activeSnapshotFilePaths = null)
        {
            try
            {
                if (_trackerSync is null || filePaths.Count == 0) return;
                QueueWorkDeltaAcrossActiveFiles(
                    totalElapsedSeconds: totalElapsedSeconds,
                    filesToExclude: null,
                    activeSnapshotFilePaths: activeSnapshotFilePaths,
                    forceEvenIfPaused: true,
                    overrideStatus: "done",
                    alwaysSend: true);

                MarkFilesInactive(filePaths);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerQueueDoneBatch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Queue "walk_out" status for selected files.
        /// </summary>
        private void TrackerQueueWalkOut(IReadOnlyList<string> filePaths, int totalElapsedSeconds, IReadOnlyList<string>? activeSnapshotFilePaths = null)
        {
            TrackerQueueQcBatchStatus(filePaths, totalElapsedSeconds, "walk_out", "TrackerQueueWalkOut", activeSnapshotFilePaths);
        }

        /// <summary>
        /// Queue "skip" status for skipped files.
        /// </summary>
        private void TrackerQueueSkip(IReadOnlyList<string> filePaths, int totalElapsedSeconds, IReadOnlyList<string>? activeSnapshotFilePaths = null)
        {
            TrackerQueueQcBatchStatus(filePaths, totalElapsedSeconds, "skip", "TrackerQueueSkip", activeSnapshotFilePaths);
        }

        private void TrackerQueueQcBatchStatus(
            IReadOnlyList<string> filePaths,
            int totalElapsedSeconds,
            string fileStatus,
            string operationName,
            IReadOnlyList<string>? activeSnapshotFilePaths)
        {
            try
            {
                if (_trackerSync is null || filePaths.Count == 0) return;

                if (string.Equals(fileStatus, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    // Update non-skipped files with time (keep them as working)
                    QueueWorkDeltaAcrossActiveFiles(totalElapsedSeconds, filesToExclude: filePaths, activeSnapshotFilePaths: activeSnapshotFilePaths);
                    
                    // Update ONLY the skipped files with NO time, but with the skipped status
                    QueueWorkDeltaAcrossActiveFiles(
                        totalElapsedSeconds: _lastSentWorkSeconds, // no extra time for skipped files
                        filesToExclude: null,
                        activeSnapshotFilePaths: filePaths,
                        forceEvenIfPaused: true,
                        overrideStatus: fileStatus,
                        alwaysSend: true);

                    MarkFilesInactive(filePaths);
                    return;
                }

                // For WalkOut
                QueueWorkDeltaAcrossActiveFiles(
                    totalElapsedSeconds: totalElapsedSeconds,
                    filesToExclude: null,
                    activeSnapshotFilePaths: activeSnapshotFilePaths, // use active selection to give them time
                    forceEvenIfPaused: true,
                    overrideStatus: fileStatus,
                    alwaysSend: true);

                MarkFilesInactive(filePaths);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] {operationName} error: {ex.Message}");
            }
        }

        // ── Helpers ──

        private string GetSelectedCategories()
        {
            try
            {
                var items = TaskMenu?.Items
                    .OfType<MenuItem>()
                    .Where(x => x.IsChecked)
                    .Select(x => (x.Header?.ToString() ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                return items is not null && items.Count > 0
                    ? string.Join(", ", items)
                    : string.Empty;
            }
            catch (Exception catEx)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedCategories error: {catEx.Message}");
                return string.Empty;
            }
        }

        private int? GetCurrentET()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_vm.ActiveJobClientCode)) return null;

                return _jobListRows
                    .FirstOrDefault(x =>
                        string.Equals(x.ClientCode, _vm.ActiveJobClientCode, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.FolderPath, _vm.ActiveJobFolderPath, StringComparison.OrdinalIgnoreCase))
                    ?.ET;
            }
            catch (Exception etEx)
            {
                System.Diagnostics.Debug.WriteLine($"GetCurrentET error: {etEx.Message}");
                return null;
            }
        }

        private string GetEffectiveWorkTypeForTracker()
        {
            try
            {
                if (_vm.HasSelectionMetaLock)
                {
                    var locked = (_vm.SelectionLockedWorkType ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(locked))
                    {
                        return locked;
                    }
                }

                return GetCurrentWorkTypeInfo().Name ?? string.Empty;
            }
            catch
            {
                return GetCurrentWorkTypeInfo().Name ?? string.Empty;
            }
        }

        private string GetEffectiveClientCodeForTracker()
        {
            try
            {
                if (_vm.HasSelectionMetaLock)
                {
                    var locked = (_vm.SelectionLockedClientCode ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(locked))
                    {
                        return locked;
                    }
                }

                return _vm.ActiveJobClientCode ?? string.Empty;
            }
            catch
            {
                return _vm.ActiveJobClientCode ?? string.Empty;
            }
        }

        private int? GetEffectiveETForTracker()
        {
            try
            {
                if (_vm.HasSelectionMetaLock && _vm.SelectionLockedEt is not null)
                {
                    return _vm.SelectionLockedEt;
                }

                return GetCurrentET();
            }
            catch
            {
                return GetCurrentET();
            }
        }

        private string GetEffectiveCategoriesForTracker()
        {
            try
            {
                if (_vm.HasSelectionMetaLock)
                {
                    var lockedTasks = _vm.SelectionLockedTasks
                        .Select(x => (x ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    if (lockedTasks.Count > 0)
                    {
                        return string.Join(", ", lockedTasks);
                    }
                }

                return GetSelectedCategories();
            }
            catch
            {
                return GetSelectedCategories();
            }
        }

        private List<PauseReasonDto>? BuildPauseReasons()
        {
            try
            {
                var list = _workSession.PauseReasonHistory
                    .Select(x => new PauseReasonDto
                    {
                        Reason = x.Reason,
                        Duration = (int)x.DurationSeconds
                    })
                    .ToList();

                // If currently paused, include the active reason immediately so backend can
                // show the selected reason and live duration even before resume.
                if (_workSession.IsPauseActive)
                {
                    var r = (_workSession.CurrentPauseReason ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(r))
                    {
                        var currentPauseSeconds = Math.Max(0, _workSession.TotalPauseTimeSeconds - _workSession.PauseTimeSeconds);
                        list.Add(new PauseReasonDto { Reason = r, Duration = (int)currentPauseSeconds });
                    }
                }

                return list.Count == 0 ? null : list;
            }
            catch (Exception preasEx)
            {
                System.Diagnostics.Debug.WriteLine($"BuildPauseReasons error: {preasEx.Message}");
                return null;
            }
        }

        private void TrackerQueueQcStatus(IReadOnlyList<string> filePaths, string fileStatus)
        {
            try
            {
                if (_trackerSync is null || filePaths.Count == 0) return;

                var workType = GetEffectiveWorkTypeForTracker();
                var categories = GetEffectiveCategoriesForTracker();
                var estimateTime = GetEffectiveETForTracker();

                var validFiles = filePaths
                    .Select(fp => (fp ?? string.Empty).Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                if (validFiles.Length > 0)
                {
                    var dto = TrackerDtoFactory.CreateQcStatusDto(
                        employeeName: Configuration.AppConfig.CurrentDisplayName,
                        workType: workType,
                        shift: ShiftDetector.GetCurrentShift(),
                        clientCode: GetEffectiveClientCodeForTracker(),
                        folderPath: GetActiveJobFolderPath(),
                        estimateTime: estimateTime,
                        categories: categories,
                        totalTimes: null,
                        fileStatus: fileStatus,
                        pauseCount: _workSession.PauseCount,
                        pauseTime: (int)_workSession.TotalPauseTimeSeconds,
                        pauseReasons: BuildPauseReasons(),
                        filePaths: validFiles,
                        perFileTimeSpent: null);

                    _trackerSync.QueueQcWorkLog(dto);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerV2.Tracker] TrackerQueueQcStatus error: {ex.Message}");
            }
        }

        private int GetWorkTimerElapsedSeconds()
        {
            try
            {
                var total = _workTimerElapsed;
                if (_workTimerRunningSince is not null)
                {
                    total += (DateTime.Now - _workTimerRunningSince.Value);
                }

                return (int)total.TotalSeconds;
            }
            catch (Exception tmrEx)
            {
                System.Diagnostics.Debug.WriteLine($"GetWorkTimerElapsedSeconds error: {tmrEx.Message}");
                return 0;
            }
        }

        private void QueueWorkDeltaAcrossActiveFiles(
            int totalElapsedSeconds,
            IReadOnlyList<string>? filesToExclude,
            IReadOnlyList<string>? activeSnapshotFilePaths = null,
            bool forceEvenIfPaused = false,
            string? overrideStatus = null,
            bool alwaysSend = false)
        {
            try
            {
                if (_trackerSync is null)
                {
                    return;
                }

                if (_vm.IsPaused && !forceEvenIfPaused)
                {
                    return;
                }

                var delta = Math.Max(0, totalElapsedSeconds - _lastSentWorkSeconds);
                if (delta <= 0 && !alwaysSend)
                {
                    return;
                }

                var activeSource = activeSnapshotFilePaths ?? GetTrackerTargetFullPaths();
                var active = FilterActiveFilePaths(activeSource);
                if (filesToExclude is not null && filesToExclude.Count > 0)
                {
                    var excludeNames = new HashSet<string>(filesToExclude.Select(NormalizeFileNameForTracker), StringComparer.OrdinalIgnoreCase);
                    active = active.Where(x => !excludeNames.Contains(NormalizeFileNameForTracker(x))).ToList();
                }

                if (active.Count == 0)
                {
                    _lastSentWorkSeconds = totalElapsedSeconds;
                    return;
                }

                // Distribute delta seconds across files with remainder so large selections
                // still receive time (e.g., 60s across 73 files => 60 files get +1, 13 get +0).
                var basePerFile = active.Count > 0 ? delta / active.Count : delta;
                var remainder = active.Count > 0 ? delta % active.Count : 0;
                var workType = GetEffectiveWorkTypeForTracker();

                var validFiles = active
                    .Select(fp => (fp ?? string.Empty).Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .OrderBy(p => NormalizeFileNameForTracker(p), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (validFiles.Length > 0)
                {
                    var dto = new SyncQcWorkLogDto
                    {
                        EmployeeName = (Configuration.AppConfig.CurrentDisplayName ?? string.Empty).ToLowerInvariant(),
                        WorkType = (workType ?? string.Empty).ToLowerInvariant(),
                        Shift = ShiftDetector.GetCurrentShift(),
                        ClientCode = GetEffectiveClientCodeForTracker(),
                        FolderPath = GetActiveJobFolderPath(),
                        EstimateTime = GetEffectiveETForTracker(),
                        Categories = GetEffectiveCategoriesForTracker(),
                        TotalTimes = delta,
                        FileStatus = overrideStatus ?? "working",
                        PauseCount = _workSession.PauseCount,
                        PauseTime = (int)_workSession.TotalPauseTimeSeconds,
                        PauseReasons = BuildPauseReasons(),
                        Files = new System.Collections.Generic.List<QcWorkLogFileDto>()
                    };

                    for (var i = 0; i < validFiles.Length; i++)
                    {
                        var add = basePerFile + (i < remainder ? 1 : 0);
                        dto.Files.Add(new QcWorkLogFileDto
                        {
                            FileName = System.IO.Path.GetFileNameWithoutExtension(validFiles[i]) ?? string.Empty,
                            TimeSpent = add <= 0 ? 0 : add,
                        });
                    }

                    _trackerSync.QueueQcWorkLog(dto);
                }

                _lastSentWorkSeconds = totalElapsedSeconds;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QueueWorkDeltaAcrossActiveFiles error: {ex.Message}");
            }
        }



        private void MarkFilesInactive(IReadOnlyList<string> filePaths)
        {
            try
            {
                foreach (var fp in filePaths)
                {
                    var n = NormalizeFileNameForTracker(fp);
                    if (!string.IsNullOrWhiteSpace(n))
                    {
                        _inactiveTrackerFiles.Add(n);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarkFilesInactive error: {ex.Message}");
            }
        }

        private List<string> FilterActiveFilePaths(IReadOnlyList<string> allFilePaths)
        {
            var list = new List<string>();
            foreach (var fp in allFilePaths)
            {
                var n = NormalizeFileNameForTracker(fp);
                if (string.IsNullOrWhiteSpace(n))
                {
                    continue;
                }

                if (_inactiveTrackerFiles.Contains(n))
                {
                    continue;
                }

                list.Add(fp);
            }

            return list;
        }

        private static string NormalizeFileNameForTracker(string? filePath)
        {
            try
            {
                var name = System.IO.Path.GetFileName(filePath ?? string.Empty);
                name = (name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name)) return string.Empty;

                try
                {
                    var ext = System.IO.Path.GetExtension(name);
                    if (!string.IsNullOrWhiteSpace(ext))
                    {
                        var baseName = System.IO.Path.GetFileNameWithoutExtension(name);
                        if (!string.IsNullOrWhiteSpace(baseName))
                        {
                            name = baseName;
                        }
                    }
                }
                catch
                {
                }

                return (name ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
