using System;
using System.Collections.Generic;

namespace SCHLStudio.App.Services.Api
{
    public sealed class ApiLoginResult
    {
        public bool Success { get; set; }
        public bool Valid { get; set; }
        public bool PasswordSetupRequired { get; set; }
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string Role { get; set; } = "Employee";
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>Non-null when the previous session ended abnormally and working files can be resumed.</summary>
        public ActiveWorkData? ActiveWork { get; set; }
    }

    /// <summary>Returned from the login endpoint when there are unfinished working files from a crashed/closed session.</summary>
    public sealed class ActiveWorkData
    {
        public string ClientCode { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public string WorkType { get; set; } = string.Empty;
        public int EstimateTime { get; set; }
        public string Categories { get; set; } = string.Empty;
        public int DoneTimeTotal { get; set; }
        public List<ActiveWorkFile> Files { get; set; } = new();
    }

    public sealed class ActiveWorkFile
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTimeOffset? StartedAt { get; set; }
        public int TimeSpent { get; set; }
    }

    public sealed class ApiSetPasswordResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ApiCheckUserResult
    {
        public bool Exists { get; set; }
        public bool PasswordRequired { get; set; } = true;
        public string? Username { get; set; }
        public string Role { get; set; } = "Employee";
        public string? Error { get; set; }
    }

    public sealed class ApiSearchResultRow
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

    public sealed class ApiSearchFileResult
    {
        public bool Success { get; set; }
        public List<ApiSearchResultRow> Results { get; set; } = new();
    }

    public sealed class ApiReportFileRequest
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string WorkType { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public string ClientCode { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string DateToday { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Report { get; set; } = string.Empty;
    }
}
