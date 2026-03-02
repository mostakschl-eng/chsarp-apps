namespace SCHLStudio.App.Views.ExplorerV2.Models
{
    public sealed class FileTileItem
    {
        public string Extension { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public string ExtensionLower { get; init; } = string.Empty;
        public string FolderName { get; init; } = string.Empty;
        public bool IsHeader { get; init; }

    }
}
