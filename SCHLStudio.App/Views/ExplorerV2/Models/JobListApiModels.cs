using System.Collections.Generic;

namespace SCHLStudio.App.Views.ExplorerV2.Models
{
    internal sealed class JobListApiResponse
    {
        public bool Success { get; set; }
        public List<JobListApiJob> Jobs { get; set; } = new();
    }

    internal sealed class JobListApiJob
    {
        public string? ClientCode { get; set; }
        public string? Folder { get; set; }
        public string? FolderPath { get; set; }
        public int Et { get; set; }
        public int Nof { get; set; }
        public string? Task { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
    }
}
