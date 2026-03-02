using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;

namespace SCHLStudio.App.ViewModels.LiveTracking.Tabs
{
    public sealed class QcTabViewModel : ViewModelBase
    {
        private readonly ObservableCollection<LiveTrackingSessionModel> _qcRows = new();
        public ReadOnlyObservableCollection<LiveTrackingSessionModel> QcRows { get; }

        // Cards
        private int _activeUsers;
        public int ActiveUsers { get => _activeUsers; private set => SetProperty(ref _activeUsers, value); }

        private int _totalFiles;
        public int TotalFiles { get => _totalFiles; private set => SetProperty(ref _totalFiles, value); }

        private int _completedFiles;
        public int CompletedFiles { get => _completedFiles; private set => SetProperty(ref _completedFiles, value); }

        private string _avgTimePerFile = "0m";
        public string AvgTimePerFile { get => _avgTimePerFile; private set => SetProperty(ref _avgTimePerFile, value); }

        public QcTabViewModel()
        {
            QcRows = new ReadOnlyObservableCollection<LiveTrackingSessionModel>(_qcRows);
        }

        public void RefreshData(System.Collections.Generic.List<LiveTrackingSessionModel> sessions)
        {
            try
            {
                if (sessions == null) return;

                // If we receive an empty refresh (often happens during transient network/socket issues),
                // keep the last known snapshot in memory instead of clearing the UI.
                if (sessions.Count == 0 && _qcRows.Count > 0)
                {
                    return;
                }

                var qcSessions = sessions
                    .Where(s => IsQcWorkType(s.WorkType))
                    .Where(s => s.IsActive)
                    .OrderByDescending(s => s.UpdatedAt)
                    .ToList();

                ActiveUsers = qcSessions.Select(s => s.EmployeeName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();
                TotalFiles = qcSessions.SelectMany(s => s.Files).Count();
                CompletedFiles = qcSessions.SelectMany(s => s.Files)
                    .Count(f => string.Equals(f.FileStatus, "done", StringComparison.OrdinalIgnoreCase));

                var totalTime = qcSessions.Sum(s => s.TotalTimes);
                AvgTimePerFile = TotalFiles > 0 ? LiveTrackingFileModel.FormatMinutes(totalTime / TotalFiles) : "0m";

                // Sync _qcRows without clearing to prevent UI flicker
                var existingKeys = _qcRows.Select(r => r.Id).ToHashSet();
                var newKeys = qcSessions.Select(r => r.Id).ToHashSet();

                // Remove orphans
                for (int i = _qcRows.Count - 1; i >= 0; i--)
                {
                    if (!newKeys.Contains(_qcRows[i].Id))
                    {
                        _qcRows.RemoveAt(i);
                    }
                }

                // Add or reposition
                for (int i = 0; i < qcSessions.Count; i++)
                {
                    var incoming = qcSessions[i];
                    var existingNode = _qcRows.FirstOrDefault(r => r.Id == incoming.Id);

                    if (existingNode == null)
                    {
                        _qcRows.Insert(i, incoming);
                    }
                    else
                    {
                        // NOTE: incoming sessions are the same objects as those already stored in _qcRows
                        // because ApplyGlobalFilters passes through _allData references.
                        // If we clear Files on the same object and then iterate incoming.Files, we end up
                        // clearing the list and losing IsActive, causing flicker/disappear.
                        if (ReferenceEquals(existingNode, incoming))
                        {
                            var existingIndex = _qcRows.IndexOf(existingNode);
                            if (existingIndex != i)
                            {
                                _qcRows.Move(existingIndex, i);
                            }
                            continue;
                        }

                        var currentIndex = _qcRows.IndexOf(existingNode);

                        existingNode.EmployeeName = incoming.EmployeeName;
                        existingNode.ClientCode = incoming.ClientCode;
                        existingNode.WorkType = incoming.WorkType;
                        existingNode.Shift = incoming.Shift;
                        existingNode.Categories = incoming.Categories;
                        existingNode.EstimateTime = incoming.EstimateTime;
                        existingNode.TotalTimes = incoming.TotalTimes;
                        existingNode.CreatedAt = incoming.CreatedAt;
                        existingNode.UpdatedAt = incoming.UpdatedAt;
                        
                        existingNode.Files.Clear();
                        foreach (var f in incoming.Files) existingNode.Files.Add(f);
                        existingNode.NotifyFilesChanged();
                        
                        if (currentIndex != i)
                        {
                            _qcRows.Move(currentIndex, i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[QcTabViewModel] RefreshData error: {ex.Message}");
            }
        }

        private static bool IsQcWorkType(string workType) =>
            (workType ?? string.Empty).Trim().ToLowerInvariant().StartsWith("qc");
    }
}
