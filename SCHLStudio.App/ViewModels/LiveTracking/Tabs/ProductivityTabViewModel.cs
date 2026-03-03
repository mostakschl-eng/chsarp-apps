using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.ViewModels.LiveTracking.Models;

namespace SCHLStudio.App.ViewModels.LiveTracking.Tabs
{
    public sealed class ProductivityTabViewModel : ViewModelBase
    {
        // Cards
        private int _totalFiles;
        public int TotalFiles { get => _totalFiles; private set => SetProperty(ref _totalFiles, value); }

        private int _totalUsers;
        public int TotalUsers { get => _totalUsers; private set => SetProperty(ref _totalUsers, value); }

        private int _completedFiles;
        public int CompletedFiles { get => _completedFiles; private set => SetProperty(ref _completedFiles, value); }

        private string _avgFilesPerUser = "0";
        public string AvgFilesPerUser { get => _avgFilesPerUser; private set => SetProperty(ref _avgFilesPerUser, value); }

        private string _avgTimePerFile = "0m";
        public string AvgTimePerFile { get => _avgTimePerFile; private set => SetProperty(ref _avgTimePerFile, value); }

        // Category→Employee groups inside the Employee box
        private readonly ObservableCollection<CategoryEmployeeGroupModel> _employeeCategoryGroups = new();
        public ReadOnlyObservableCollection<CategoryEmployeeGroupModel> EmployeeCategoryGroups { get; }

        // Category→Employee groups inside the QC box
        private readonly ObservableCollection<CategoryEmployeeGroupModel> _qcCategoryGroups = new();
        public ReadOnlyObservableCollection<CategoryEmployeeGroupModel> QcCategoryGroups { get; }

        // ─── Search ───
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    RebuildAll();
            }
        }

        // ─── Date filter ───
        private DateTime? _filterDateFrom;
        public DateTime? FilterDateFrom
        {
            get => _filterDateFrom;
            set => SetProperty(ref _filterDateFrom, value);
        }

        private DateTime? _filterDateTo;
        public DateTime? FilterDateTo
        {
            get => _filterDateTo;
            set => SetProperty(ref _filterDateTo, value);
        }

        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }

        // ─── Last data for re-filtering ───
        private List<LiveTrackingSessionModel> _lastSessions = new();

        public ProductivityTabViewModel()
        {
            EmployeeCategoryGroups = new ReadOnlyObservableCollection<CategoryEmployeeGroupModel>(_employeeCategoryGroups);
            QcCategoryGroups = new ReadOnlyObservableCollection<CategoryEmployeeGroupModel>(_qcCategoryGroups);
            ApplyFilterCommand = new RelayCommand(_ => RebuildAll());
            ClearFilterCommand = new RelayCommand(_ => ClearFilter());
        }

        public void RefreshData(List<LiveTrackingSessionModel> allSessions)
        {
            try
            {
                _lastSessions = allSessions ?? new List<LiveTrackingSessionModel>();
                RebuildAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductivityTabViewModel] RefreshData error: {ex.Message}");
            }
        }

        private void RebuildAll()
        {
            try
            {
                var sessions = ApplyDateFilter(_lastSessions);
                var filtered = ApplySearchFilter(sessions);

                // Summary Cards (based on filtered data)
                TotalFiles = filtered.SelectMany(s => s.Files).Count();
                TotalUsers = filtered.Select(s => s.EmployeeName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();
                AvgFilesPerUser = TotalUsers > 0 ? Math.Round((double)TotalFiles / TotalUsers, 1).ToString("0.#") : "0";

                var completed = filtered.SelectMany(s => s.Files)
                    .Where(f => string.Equals(f.FileStatus, "done", StringComparison.OrdinalIgnoreCase))
                    .Select(f => (f.FileName ?? string.Empty).ToLowerInvariant())
                    .Distinct()
                    .Count();
                CompletedFiles = completed;
                var totalTime = filtered.Sum(s => s.TotalTimes);
                AvgTimePerFile = completed > 0 ? LiveTrackingFileModel.FormatMinutes(totalTime / completed) : "0m";

                // Build category→employee groups for PRODUCTION (non-QC)
                var prodSessions = filtered.Where(s => !IsQcWorkType(s.WorkType)).ToList();
                var prodCategoryGroups = BuildCategoryGroups(prodSessions);
                SyncCategoryGroups(_employeeCategoryGroups, prodCategoryGroups);

                // Build category→employee groups for QC
                var qcSessions = filtered.Where(s => IsQcWorkType(s.WorkType)).ToList();
                var qcCategoryGroups = BuildCategoryGroups(qcSessions);
                SyncCategoryGroups(_qcCategoryGroups, qcCategoryGroups);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductivityTabViewModel] RebuildAll error: {ex.Message}");
            }
        }

        private List<LiveTrackingSessionModel> ApplyDateFilter(List<LiveTrackingSessionModel> sessions)
        {
            if (!_filterDateFrom.HasValue && !_filterDateTo.HasValue)
                return sessions;

            var from = (_filterDateFrom ?? _filterDateTo)?.Date ?? DateTime.Today;
            var to = ((_filterDateTo ?? _filterDateFrom)?.Date ?? DateTime.Today).AddDays(1);

            return sessions.Where(s =>
            {
                var dt = s.UpdatedAt.ToLocalTime().Date;
                return dt >= from && dt < to;
            }).ToList();
        }

        private List<LiveTrackingSessionModel> ApplySearchFilter(List<LiveTrackingSessionModel> sessions)
        {
            var query = (_searchText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return sessions;

            return sessions.Where(s =>
                (s.EmployeeName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();
        }

        private void ClearFilter()
        {
            FilterDateFrom = null;
            FilterDateTo = null;
            SearchText = string.Empty;
            RebuildAll();
        }

        private static List<CategoryEmployeeGroupModel> BuildCategoryGroups(List<LiveTrackingSessionModel> sessions)
        {
            // Flatten sessions by category (each session can have multiple categories)
            var catEmployeePairs = sessions
                .Where(s => !string.IsNullOrWhiteSpace(s.Categories))
                .SelectMany(s =>
                {
                    var cats = s.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    return cats.Select(c => new { Category = c.Trim(), Session = s });
                })
                .ToList();

            return catEmployeePairs
                .GroupBy(x => x.Category.ToLowerInvariant())
                .Select(catGroup =>
                {
                    var categoryName = catGroup.First().Category;
                    var employees = catGroup
                        .GroupBy(x => x.Session.EmployeeName?.Trim().ToLowerInvariant() ?? "")
                        .Select(empGroup => new ProductivityUserModel
                        {
                            EmployeeName = empGroup.First().Session.EmployeeName,
                            WorkType = string.Join(", ", empGroup.Select(x => x.Session.WorkType).Where(w => !string.IsNullOrWhiteSpace(w)).Distinct()),
                            TotalFiles = empGroup.Sum(x => x.Session.Files.Count),
                            CompletedFiles = empGroup.Sum(x => x.Session.Files.Count(f => string.Equals(f.FileStatus, "done", StringComparison.OrdinalIgnoreCase))),
                            TotalTime = empGroup.Sum(x => x.Session.TotalTimes)
                        })
                        .OrderByDescending(e => e.TotalFiles)
                        .ThenBy(e => e.TotalFiles > 0 ? e.TotalTime / e.TotalFiles : double.MaxValue)
                        .ToList();

                    return new CategoryEmployeeGroupModel
                    {
                        Category = categoryName,
                        Employees = new ObservableCollection<ProductivityUserModel>(employees)
                    };
                })
                .OrderByDescending(g => g.Employees.Sum(e => e.CompletedFiles))
                .ToList();
        }

        private static void SyncCategoryGroups(ObservableCollection<CategoryEmployeeGroupModel> target, List<CategoryEmployeeGroupModel> source)
        {
            var existingKeys = target.Select(c => c.Category).ToHashSet();
            var newKeys = source.Select(c => c.Category).ToHashSet();

            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!newKeys.Contains(target[i].Category)) target.RemoveAt(i);
            }

            for (int i = 0; i < source.Count; i++)
            {
                var incomingCat = source[i];
                var existingCat = target.FirstOrDefault(c => c.Category == incomingCat.Category);

                if (existingCat == null)
                {
                    // Copy list to observable
                    var newCatGroup = new CategoryEmployeeGroupModel { Category = incomingCat.Category };
                    foreach(var emp in incomingCat.Employees) newCatGroup.Employees.Add(emp);
                    target.Insert(i, newCatGroup);
                }
                else
                {
                    // Sync inner employees
                    var exEmpKeys = existingCat.Employees.Select(e => e.EmployeeName).ToHashSet();
                    var inEmpKeys = incomingCat.Employees.Select(e => e.EmployeeName).ToHashSet();

                    for (int j = existingCat.Employees.Count - 1; j >= 0; j--)
                    {
                        if (!inEmpKeys.Contains(existingCat.Employees[j].EmployeeName))
                            existingCat.Employees.RemoveAt(j);
                    }

                    for (int j = 0; j < incomingCat.Employees.Count; j++)
                    {
                        var incomingEmp = incomingCat.Employees[j];
                        var existingEmp = existingCat.Employees.FirstOrDefault(e => e.EmployeeName == incomingEmp.EmployeeName);

                        if (existingEmp == null)
                        {
                            existingCat.Employees.Insert(j, incomingEmp);
                        }
                        else
                        {
                            existingEmp.WorkType = incomingEmp.WorkType;
                            existingEmp.TotalFiles = incomingEmp.TotalFiles;
                            existingEmp.CompletedFiles = incomingEmp.CompletedFiles;
                            existingEmp.TotalTime = incomingEmp.TotalTime;

                            var cIdx = existingCat.Employees.IndexOf(existingEmp);
                            if (cIdx != j) existingCat.Employees.Move(cIdx, j);
                        }
                    }

                    var currentIndex = target.IndexOf(existingCat);
                    if (currentIndex != i) target.Move(currentIndex, i);
                }
            }
        }

        private static bool IsQcWorkType(string workType) =>
            (workType ?? string.Empty).Trim().ToLowerInvariant().Contains("qc");
    }
}
