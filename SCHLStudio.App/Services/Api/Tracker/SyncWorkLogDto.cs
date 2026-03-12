using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SCHLStudio.App.Services.Api.Tracker
{
    /// <summary>
    /// Matches backend SyncQcWorkLogDto (camelCase serialization).
    /// Used for QC sync via POST /tracker/sync-qc.
    /// Batch-level fields (categories, fileStatus, totalTimes, pause*) live at root.
    /// </summary>
    public sealed class SyncQcWorkLogDto
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string FileStatus { get; set; } = string.Empty;
        public string? WorkType { get; set; }
        public string? Shift { get; set; }
        public string? ClientCode { get; set; }
        public string? FolderPath { get; set; }
        public int? EstimateTime { get; set; }
        public string? Categories { get; set; }
        public int? TotalTimes { get; set; }
        public int? PauseCount { get; set; }
        public int? PauseTime { get; set; }
        public List<PauseReasonDto>? PauseReasons { get; set; }
        public string? SyncId { get; set; }
        public List<QcWorkLogFileDto> Files { get; set; } = new();
    }

    public sealed class QcWorkLogFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public int? TimeSpent { get; set; }

        [JsonIgnore]
        public string? StartedAt { get; set; }

        [JsonIgnore]
        public string? CompletedAt { get; set; }
    }

    public sealed class PauseReasonDto
    {
        public string Reason { get; set; } = string.Empty;
        public int Duration { get; set; }
    }
}
