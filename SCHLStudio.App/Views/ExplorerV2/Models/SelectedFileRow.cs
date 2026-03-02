namespace SCHLStudio.App.Views.ExplorerV2.Models
{
    public sealed class SelectedFileRow
    {
        public int Serial { get; set; }
        public string FullPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DisplayFileName { get; set; } = string.Empty;
    }
}
