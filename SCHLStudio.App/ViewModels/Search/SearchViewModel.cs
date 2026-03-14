using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.Views.Dialogs;

namespace SCHLStudio.App.ViewModels.Search
{
    internal sealed class SearchViewModel : ViewModelBase
    {
        private readonly IApiClient? _api;
        private string _searchQuery = string.Empty;
        private bool _hasSearched;
        private bool _isSearching;
        private CancellationTokenSource? _searchCts;

        public ObservableCollection<SearchResultRow> Results { get; } = new();

        public bool HasSearched
        {
            get => _hasSearched;
            private set
            {
                if (SetProperty(ref _hasSearched, value))
                {
                    OnPropertyChanged(nameof(ShowResults));
                    OnPropertyChanged(nameof(ShowNoResults));
                }
            }
        }

        public bool HasResults => Results.Count > 0;

        public bool ShowResults => HasSearched && !IsSearching && HasResults;

        public bool ShowNoResults => HasSearched && !IsSearching && !HasResults;

        public bool IsSearching
        {
            get => _isSearching;
            private set
            {
                if (SetProperty(ref _isSearching, value))
                {
                    OnPropertyChanged(nameof(ShowResults));
                    OnPropertyChanged(nameof(ShowNoResults));
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public RelayCommand SearchCommand { get; }

        public RelayCommand ClearCommand { get; }

        public RelayCommand ReportCommand { get; }

        public SearchViewModel(IApiClient? api)
        {
            _api = api;

            Results.CollectionChanged += Results_CollectionChanged;

            SearchCommand = new RelayCommand(_ =>
            {
                _ = SearchAsync();
            });
            ClearCommand = new RelayCommand(_ =>
            {
                try
                {
                    _searchCts?.Cancel();
                }
                catch (Exception ex)
                {
                    LogNonCritical("Clear.Cancel", ex);
                }

                SearchQuery = string.Empty;
                Results.Clear();
                HasSearched = false;
                IsSearching = false;
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ShowResults));
                OnPropertyChanged(nameof(ShowNoResults));
            });
            ReportCommand = new RelayCommand(p =>
            {
                _ = ReportAsync(p as SearchResultRow);
            });
        }

        private void Results_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ShowResults));
            OnPropertyChanged(nameof(ShowNoResults));
        }

        private async Task SearchAsync()
        {
            try
            {
                var q = NormalizeQuery(SearchQuery);

                HasSearched = true;
                IsSearching = true;

                if (string.IsNullOrWhiteSpace(q))
                {
                    await RunOnUiAsync(() =>
                    {
                        Results.Clear();
                        IsSearching = false;
                    }).ConfigureAwait(false);
                    return;
                }

                try
                {
                    _searchCts?.Cancel();
                }
                catch
                {
                }

                try
                {
                    _searchCts?.Dispose();
                }
                catch (Exception ex)
                {
                    LogNonCritical("Search.DisposeCts", ex);
                }

                _searchCts = new CancellationTokenSource();
                var ct = _searchCts.Token;

                if (_api is null || !_api.IsAuthenticated)
                {
                    await RunOnUiAsync(() =>
                    {
                        Results.Clear();
                        IsSearching = false;
                    }).ConfigureAwait(false);
                    return;
                }

                var result = await _api.SearchFileTypedAsync(q, null, ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested)
                {
                    await RunOnUiAsync(() => IsSearching = false).ConfigureAwait(false);
                    return;
                }

                if (result is null)
                {
                    await RunOnUiAsync(() =>
                    {
                        Results.Clear();
                        IsSearching = false;
                    }).ConfigureAwait(false);
                    return;
                }
                var rows = new List<SearchResultRow>();
                try
                {
                    foreach (var item in result.Results ?? new List<ApiSearchResultRow>())
                    {
                        rows.Add(new SearchResultRow
                        {
                            FileName = item.FileName ?? string.Empty,
                            EmployeeName = CapitalizeWords(item.EmployeeName),
                            WorkType = FormatWorkType(item.WorkType),
                            Shift = CapitalizeFirst(item.Shift),
                            ClientName = (item.ClientName ?? string.Empty).ToUpperInvariant(),
                            ClientCode = (item.ClientCode ?? string.Empty).ToUpperInvariant(),
                            TimeSpent = item.TimeSpent ?? string.Empty,
                            FilePath = item.FilePath ?? string.Empty,
                            FolderPath = item.FolderPath ?? string.Empty,
                            DateToday = item.DateToday ?? string.Empty,
                            Report = item.Report ?? string.Empty,
                            FileStatus = CapitalizeFirst(item.FileStatus),
                            StartedAt = FormatTimestamp(item.StartedAt),
                            CompletedAt = FormatTimestamp(item.CompletedAt),
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogNonCritical("Search.MapRows", ex);
                    rows = new List<SearchResultRow>();
                }

                await RunOnUiAsync(() =>
                {
                    try
                    {
                        Results.Clear();
                        foreach (var r in rows)
                        {
                            if (r is not null)
                            {
                                Results.Add(r);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogNonCritical("Search.ApplyResults", ex);
                    }

                    IsSearching = false;
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await RunOnUiAsync(() => IsSearching = false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(SearchAsync), ex);
                await RunOnUiAsync(() => IsSearching = false).ConfigureAwait(false);
            }
        }

        private static async Task RunOnUiAsync(Action action)
        {
            try
            {
                var app = System.Windows.Application.Current;
                var dispatcher = app?.Dispatcher;

                if (dispatcher is null || dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                await dispatcher.InvokeAsync(action);
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(RunOnUiAsync), ex);
            }
        }

        private static string NormalizeQuery(string? query)
        {
            try
            {
                var raw = (query ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return string.Empty;
                }

                var fileOnly = raw.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? raw;

                var dot = fileOnly.LastIndexOf('.');
                if (dot <= 0 || dot >= fileOnly.Length - 1)
                {
                    return fileOnly.Trim();
                }

                return fileOnly[..dot].Trim();
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(NormalizeQuery), ex);
                return (query ?? string.Empty).Trim();
            }
        }

        private static string FormatTimestamp(string? iso)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(iso)) return string.Empty;
                if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
                    return local.ToString("dd MMM hh:mm tt");
                }
                return iso.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string CapitalizeFirst(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Length == 0) return string.Empty;
            return char.ToUpper(s[0]) + s[1..];
        }

        private static string CapitalizeWords(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Length == 0) return string.Empty;
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i][1..];
            }
            return string.Join(' ', parts);
        }

        private static string FormatWorkType(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Equals("qc ac", StringComparison.OrdinalIgnoreCase)) return "QC AC";
            if (s.Equals("qc 1", StringComparison.OrdinalIgnoreCase)) return "QC 1";
            if (s.Equals("qc 2", StringComparison.OrdinalIgnoreCase)) return "QC 2";
            return CapitalizeWords(s);
        }

        private async Task ReportAsync(SearchResultRow? row)
        {
            try
            {
                if (row is null)
                {
                    return;
                }

                var current = row.Report ?? string.Empty;
                var win = new CommentDialogWindow(current)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };
                var ok = win.ShowDialog();
                if (ok == true)
                {
                    var reportText = (win.CommentText ?? string.Empty).Trim();
                    var request = new ApiReportFileRequest
                    {
                        EmployeeName = row.EmployeeName ?? string.Empty,
                        WorkType = row.WorkType ?? string.Empty,
                        Shift = row.Shift ?? string.Empty,
                        ClientCode = row.ClientCode ?? string.Empty,
                        FolderPath = row.FolderPath ?? string.Empty,
                        DateToday = row.DateToday ?? string.Empty,
                        FileName = row.FileName ?? string.Empty,
                        Report = reportText
                    };

                    if (_api is null || !_api.IsAuthenticated)
                    {
                        return;
                    }

                    var saved = await _api.ReportFileTypedAsync(request).ConfigureAwait(false);
                    if (!saved)
                    {
                        return;
                    }

                    await RunOnUiAsync(() => row.Report = reportText).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(ReportAsync), ex);
            }
        }

        private static void LogNonCritical(string operation, Exception ex)
        {
            try
            {
                Debug.WriteLine($"[SearchViewModel] {operation} non-critical: {ex.Message}");
            }
            catch
            {
            }
        }

    }
}
