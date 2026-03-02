using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal sealed class PhotoshopLauncher
    {
        public const string AutoKey = "auto";
        public const string Ps2026Key = "ps26";
        public const string Ps2025Key = "ps25";
        public const string PsCcKey = "pscc";

        private static readonly (string Key, string DisplayName, string ExePath)[] KnownVersions =
        [
            (AutoKey, "Auto (Windows default)", string.Empty),
            (Ps2026Key, "Photoshop 26", @"C:\Program Files\Adobe\Adobe Photoshop 2026\Photoshop.exe"),
            (Ps2025Key, "Photoshop 25", @"C:\Program Files\Adobe\Adobe Photoshop 2025\Photoshop.exe"),
            (PsCcKey, "Photoshop CC", @"C:\Program Files\Adobe\Adobe Photoshop CC (64 Bit)\Photoshop.exe")
        ];

        public IReadOnlyList<(string Key, string DisplayName, string? ExePath, bool IsAvailable)> GetAvailableVersions()
        {
            try
            {
                return KnownVersions
                    .Select(v =>
                    {
                        if (string.Equals(v.Key, AutoKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return (v.Key, v.DisplayName, (string?)null, true);
                        }

                        return (v.Key, v.DisplayName, (string?)v.ExePath, File.Exists(v.ExePath));
                    })
                    .ToList();
            }
            catch
            {
                return new List<(string, string, string?, bool)>
                {
                    (AutoKey, "Auto (Windows default)", null, true)
                };
            }
        }

        public void OpenFiles(string versionKey, IEnumerable<string> filePaths)
        {
            try
            {
                var paths = (filePaths ?? Array.Empty<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (paths.Count == 0)
                {
                    return;
                }

                var key = (versionKey ?? string.Empty).Trim().ToLowerInvariant();
                if (key.Length == 0)
                {
                    key = AutoKey;
                }

                if (key == AutoKey)
                {
                    foreach (var p in paths)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                        }
                        catch (Exception ex_safe_log)
                        {
                            NonCriticalLog.EnqueueError("ExplorerV2", "PhotoshopLauncher", ex_safe_log);
                        }
                    }

                    return;
                }

                var exe = KnownVersions.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)).ExePath;
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                {
                    foreach (var p in paths)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
                        }
                        catch (Exception ex_safe_log)
                        {
                            NonCriticalLog.EnqueueError("ExplorerV2", "PhotoshopLauncher", ex_safe_log);
                        }
                    }

                    return;
                }

                var args = string.Join(" ", paths.Select(p => $"\"{p}\""));
                Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
            }
            catch (Exception ex_safe_log)
            {
                NonCriticalLog.EnqueueError("ExplorerV2", "PhotoshopLauncher", ex_safe_log);
            }
        }
    }
}
