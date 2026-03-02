using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Services.Api.Tracker
{
    /// <summary>
    /// Orchestrates the tracker queue and sync worker.
    /// Single instance per ExplorerV2 session.
    /// </summary>
    public sealed class TrackerSyncService
    {
        private readonly TrackerLocalQueue _queue;
        private readonly TrackerSyncWorker _worker;
        private bool _started;

        public TrackerSyncService(HttpClient httpClient, string apiBaseUrl, Func<bool> isAuthenticated, string? userName)
        {
            var user = (userName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(user)) user = "_global";
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                user = user.Replace(c, '_');
            }

            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var queueDir = ResolveQueueDir(user, date);
            var queuePath = Path.Combine(queueDir, "sync_queue.json");

            _queue = new TrackerLocalQueue(queuePath);
            _queue.SanitizeLingeringWorkingFiles();

            _worker = new TrackerSyncWorker(_queue, httpClient, apiBaseUrl, isAuthenticated);
        }

        private static string ResolveQueueDir(string user, string date)
        {
            var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localDir = Path.Combine(localRoot, "SCHLStudio", user, date, "queue");
            
            try
            {
                Directory.CreateDirectory(localDir);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Queue.ResolveDir.CreateLocal",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["localDir"] = localDir
                        });
                }
                catch { }
            }
            
            return localDir;
        }

        /// <summary>
        /// Start the background sync worker.
        /// Safe to call multiple times — only starts once.
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            _worker.Start();
            Debug.WriteLine("[TrackerService] Started");
        }

        /// <summary>
        /// Stop the background sync worker gracefully.
        /// </summary>
        public void Stop()
        {
            if (!_started) return;
            _started = false;
            _worker.Stop();
            Debug.WriteLine("[TrackerService] Stopped");
        }

        /// <summary>
        /// Queue a batch work log (QC 1, QC 2, QC AC).
        /// </summary>
        public void QueueQcWorkLog(SyncQcWorkLogDto dto)
        {
            try
            {
                _queue.Enqueue(dto);
                _worker.TriggerSync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerService] QueueQcWorkLog failed: {ex.Message}");
                try
                {
                    AppDataLog.LogError(
                        area: "Tracker",
                        operation: "Service.QueueQcWorkLog",
                        ex: ex);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Number of items waiting in the queue.
        /// </summary>
        public int PendingCount => _queue.Count;

        /// <summary>
        /// Request immediate sync attempt.
        /// </summary>
        public void TriggerSync()
        {
            try
            {
                _worker.TriggerSync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrackerService] TriggerSync non-critical: {ex.Message}");
                NonCriticalLog.IncrementAndLog("Tracker", "Service.TriggerSync", ex);
            }
        }
    }
}
