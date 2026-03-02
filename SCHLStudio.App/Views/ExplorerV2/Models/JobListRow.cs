using System;
using System.Linq;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2.Models
{
    public sealed class JobListRow
    {
        public int RowNumber { get; set; }
        public string ClientCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int ET { get; set; }
        public int NOF { get; set; }
        public string Task { get; set; } = string.Empty;
        
        private string _folderPath = string.Empty;
        public string FolderPath
        {
            get => _folderPath;
            set
            {
                _folderPath = value;
                FolderPathShort = CalculateShortPath(value);
            }
        }

        public string FolderPathShort { get; private set; } = string.Empty;

        private static string CalculateShortPath(string p)
        {
            try
            {
                p = (p ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(p)) return string.Empty;

                try
                {
                    var parts = p
                        .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => (x ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToArray();

                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        var seg = parts[i];
                        if (IsDateLikeFolderName(seg))
                        {
                            var next = parts[i + 1];
                            if (!string.IsNullOrWhiteSpace(next))
                            {
                                return next;
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "JobListRow", ex_safe_log);
                }

                const int head = 15;
                const int tail = 15;
                const string ellipsis = "...";

                if (p.Length <= head + tail + ellipsis.Length)
                {
                    return p;
                }

                return p[..head] + ellipsis + p[^tail..];
            }
            catch
            {
                return p ?? string.Empty;
            }
        }

        private static bool IsDateLikeFolderName(string? folderName)
        {
            try
            {
                var s = (folderName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(s)) return false;

                var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return false;

                if (!int.TryParse(parts[0], out var day) || day < 1 || day > 31) return false;

                var month = parts[1].Trim();
                if (month.Length < 3 || month.Length > 10) return false;

                if (!int.TryParse(parts[2], out var yy) || yy < 0 || yy > 99) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsAdded { get; set; }
    }
}
