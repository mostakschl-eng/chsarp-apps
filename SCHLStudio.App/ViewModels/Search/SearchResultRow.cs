namespace SCHLStudio.App.ViewModels.Search
{
    internal sealed class SearchResultRow
    {
        public string FileName { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string WorkType { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientCode { get; set; } = string.Empty;
        public string TimeSpent { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        public string FolderPath { get; set; } = string.Empty;
        public string DateToday { get; set; } = string.Empty;

        public string Report { get; set; } = string.Empty;
    }
}
