using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SCHLStudio.App.Services.Api.Tracker
{
    public static class TrackerDtoFactory
    {
        private static string NormalizeFileName(string? filePath)
        {
            try
            {
                var name = Path.GetFileName(filePath ?? string.Empty);
                name = (name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name)) return string.Empty;

                try
                {
                    var ext = Path.GetExtension(name);
                    if (!string.IsNullOrWhiteSpace(ext))
                    {
                        var baseName = Path.GetFileNameWithoutExtension(name);
                        if (!string.IsNullOrWhiteSpace(baseName))
                        {
                            name = baseName;
                        }
                    }
                }
                catch
                {
                }

                return (name ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static SyncQcWorkLogDto CreateQcDoneDto(
            string employeeName,
            string workType,
            string shift,
            string clientCode,
            string folderPath,
            int? estimateTime,
            string categories,
            int totalTimes,
            IReadOnlyList<string> filePaths,
            int perFileTime,
            string fileStatus = "done")
        {
            return new SyncQcWorkLogDto
            {
                EmployeeName = (employeeName ?? string.Empty).ToLowerInvariant(),
                WorkType = (workType ?? string.Empty).ToLowerInvariant(),
                Shift = shift,
                ClientCode = clientCode,
                FolderPath = folderPath,
                EstimateTime = estimateTime,
                Categories = categories,
                FileStatus = fileStatus,
                TotalTimes = totalTimes,
                Files = filePaths.Select(fp => new QcWorkLogFileDto
                {
                    FileName = NormalizeFileName(fp),
                    FilePath = (fp ?? string.Empty).Trim(),
                    TimeSpent = perFileTime,
                }).ToList()
            };
        }

        public static SyncQcWorkLogDto CreateQcStatusDto(
            string employeeName,
            string workType,
            string shift,
            string clientCode,
            string folderPath,
            int? estimateTime,
            string? categories,
            int? totalTimes,
            string fileStatus,
            IReadOnlyList<string> filePaths,
            int? perFileTimeSpent = null)
        {
            return new SyncQcWorkLogDto
            {
                EmployeeName = (employeeName ?? string.Empty).ToLowerInvariant(),
                WorkType = (workType ?? string.Empty).ToLowerInvariant(),
                Shift = shift,
                ClientCode = clientCode,
                FolderPath = folderPath,
                EstimateTime = estimateTime,
                Categories = categories,
                TotalTimes = totalTimes,
                FileStatus = fileStatus,
                Files = filePaths.Select(fp => new QcWorkLogFileDto
                {
                    FileName = NormalizeFileName(fp),
                    FilePath = (fp ?? string.Empty).Trim(),
                    TimeSpent = perFileTimeSpent,
                }).ToList(),
            };
        }

        public static SyncPauseDto CreatePauseDto(
            string employeeName,
            string status,
            string? reason,
            string workType,
            string shift,
            string clientCode,
            string folderPath,
            int? totalTimes)
        {
            return new SyncPauseDto
            {
                EmployeeName = (employeeName ?? string.Empty).ToLowerInvariant(),
                Status = (status ?? string.Empty).Trim(),
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                WorkType = (workType ?? string.Empty).ToLowerInvariant(),
                Shift = shift,
                ClientCode = clientCode,
                FolderPath = folderPath,
                TotalTimes = totalTimes,
            };
        }
    }
}
