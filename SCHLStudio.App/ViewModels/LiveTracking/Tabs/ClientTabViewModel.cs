using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;

namespace SCHLStudio.App.ViewModels.LiveTracking.Tabs
{
    public sealed class ClientTabViewModel : ViewModelBase
    {
        private readonly ObservableCollection<ClientTabRowModel> _workingRows = new();
        public ReadOnlyObservableCollection<ClientTabRowModel> WorkingClients { get; }

        private readonly ObservableCollection<ClientTabRowModel> _inactiveRows = new();
        public ReadOnlyObservableCollection<ClientTabRowModel> InactiveClients { get; }

        // Cards
        private int _activeClients;
        public int ActiveClients { get => _activeClients; private set => SetProperty(ref _activeClients, value); }

        private int _totalEmployees;
        public int TotalEmployees { get => _totalEmployees; private set => SetProperty(ref _totalEmployees, value); }

        private int _filesCompleted;
        public int FilesCompleted { get => _filesCompleted; private set => SetProperty(ref _filesCompleted, value); }

        private string _totalTimeSpent = "0m";
        public string TotalTimeSpent { get => _totalTimeSpent; private set => SetProperty(ref _totalTimeSpent, value); }

        public ClientTabViewModel()
        {
            WorkingClients = new ReadOnlyObservableCollection<ClientTabRowModel>(_workingRows);
            InactiveClients = new ReadOnlyObservableCollection<ClientTabRowModel>(_inactiveRows);
        }

        public void RefreshData(System.Collections.Generic.List<LiveTrackingSessionModel> sessions)
        {
            try
            {
                if (sessions == null) return;

                // If we receive an empty refresh (often happens during transient network/socket issues),
                // keep the last known snapshot in memory instead of clearing the UI.
                if (sessions.Count == 0 && (_workingRows.Count > 0 || _inactiveRows.Count > 0))
                {
                    return;
                }

                var allSessions = sessions.Where(s => s != null).ToList();
                var activeSessions = allSessions.Where(s => s.IsActive).ToList();

                ActiveClients = activeSessions
                    .Where(s => !string.IsNullOrWhiteSpace(s.ClientCode))
                    .Select(s => s.ClientCode.Trim().ToUpperInvariant())
                    .Distinct().Count();

                TotalEmployees = activeSessions.Select(s => s.EmployeeName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();

                FilesCompleted = allSessions
                    .SelectMany(s => s.Files.Select(f => new
                    {
                        Folder = (s.FolderPath ?? string.Empty).Trim().ToLowerInvariant(),
                        File = (f.FileName ?? string.Empty).Trim().ToLowerInvariant(),
                        Status = f.FileStatus ?? string.Empty,
                    }))
                    .Where(x => string.Equals(x.Status, "done", StringComparison.OrdinalIgnoreCase))
                    .Select(x => $"{x.Folder}\\{x.File}")
                    .Distinct()
                    .Count();

                TotalTimeSpent = LiveTrackingFileModel.FormatMinutes(allSessions.Sum(s => s.TotalTimes));

                var grouped = allSessions
                    .Where(s => !string.IsNullOrWhiteSpace(s.ClientCode))
                    .GroupBy(s => s.ClientCode.Trim().ToUpperInvariant())
                    .Select(g =>
                    {
                        var activeCount = g.Count(s => s.IsActive);
                        return new ClientTabRowModel
                        {
                            ClientName = g.First().ClientCode,
                            ActiveEmployees = g.Where(s => s.IsActive).Select(s => s.EmployeeName).Distinct().Count(),
                            IsActive = activeCount > 0,
                            Categories = string.Join(", ", g.Select(s => s.Categories).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()),
                            TotalProductionFilesDone = g
                                .Where(s => !IsQcWorkType(s.WorkType))
                                .SelectMany(s => s.Files.Select(f => new
                                {
                                    Folder = (s.FolderPath ?? string.Empty).Trim().ToLowerInvariant(),
                                    File = (f.FileName ?? string.Empty).Trim().ToLowerInvariant(),
                                    Status = f.FileStatus ?? string.Empty,
                                }))
                                .Where(x => string.Equals(x.Status, "done", StringComparison.OrdinalIgnoreCase))
                                .Select(x => $"{x.Folder}\\{x.File}")
                                .Distinct()
                                .Count(),
                            TotalQcFilesDone = g
                                .Where(s => IsQcWorkType(s.WorkType))
                                .SelectMany(s => s.Files.Select(f => new
                                {
                                    Folder = (s.FolderPath ?? string.Empty).Trim().ToLowerInvariant(),
                                    File = (f.FileName ?? string.Empty).Trim().ToLowerInvariant(),
                                    Status = f.FileStatus ?? string.Empty,
                                }))
                                .Where(x => string.Equals(x.Status, "done", StringComparison.OrdinalIgnoreCase))
                                .Select(x => $"{x.Folder}\\{x.File}")
                                .Distinct()
                                .Count(),
                            EstimateTime = g
                                .GroupBy(s => new
                                {
                                    Folder = (s.FolderPath ?? string.Empty).Trim().ToLowerInvariant(),
                                    WorkType = (s.WorkType ?? string.Empty).Trim().ToLowerInvariant(),
                                    Category = (s.Categories ?? string.Empty).Trim().ToLowerInvariant(),
                                })
                                .Select(jobGroup => jobGroup.Max(x => x.EstimateTime))
                                .Sum(),
                            TotalTimeSpent = g.Sum(s => s.TotalTimes),
                            StartTime = g.Min(s => s.CreatedAt),
                            EndTime = g.Max(s => s.UpdatedAt),
                        };
                    })
                    .OrderByDescending(g => g.IsActive)
                    .ThenByDescending(g => g.EndTime)
                    .ToList();

                var workingIncoming = grouped.Where(g => g.IsActive).ToList();
                var inactiveIncoming = grouped.Where(g => !g.IsActive).ToList();

                SyncCollection(_workingRows, workingIncoming);
                SyncCollection(_inactiveRows, inactiveIncoming);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClientTabViewModel] RefreshData error: {ex.Message}");
            }
        }

        private void SyncCollection(ObservableCollection<ClientTabRowModel> collection, System.Collections.Generic.List<ClientTabRowModel> incoming)
        {
            var newKeys = incoming.Select(r => r.ClientName).ToHashSet();

            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!newKeys.Contains(collection[i].ClientName))
                    collection.RemoveAt(i);
            }

            for (int i = 0; i < incoming.Count; i++)
            {
                var data = incoming[i];
                var existing = collection.FirstOrDefault(r => r.ClientName == data.ClientName);

                if (existing == null)
                {
                    collection.Insert(i, data);
                }
                else
                {
                    existing.ActiveEmployees = data.ActiveEmployees;
                    existing.IsActive = data.IsActive;
                    existing.Categories = data.Categories;
                    existing.TotalProductionFilesDone = data.TotalProductionFilesDone;
                    existing.TotalQcFilesDone = data.TotalQcFilesDone;
                    existing.EstimateTime = data.EstimateTime;
                    existing.TotalTimeSpent = data.TotalTimeSpent;
                    existing.StartTime = data.StartTime;
                    existing.EndTime = data.EndTime;

                    var currentIndex = collection.IndexOf(existing);
                    if (currentIndex != i) collection.Move(currentIndex, i);
                }
            }
        }

        private static bool IsQcWorkType(string workType) =>
            (workType ?? string.Empty).Trim().ToLowerInvariant().StartsWith("qc");
    }
}
