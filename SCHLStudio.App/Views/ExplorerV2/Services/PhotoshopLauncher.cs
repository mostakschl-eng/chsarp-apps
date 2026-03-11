using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal sealed class PhotoshopLauncher
    {
        private const int LaunchDelayMilliseconds = 400;

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
                var paths = ResolveLaunchPaths(filePaths);

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
                    LaunchViaShell(paths);

                    return;
                }

                var exe = KnownVersions.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)).ExePath;
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                {
                    ShowPhotoshopUnavailableMessage(key);

                    return;
                }

                LaunchViaPhotoshopExe(exe, paths);
            }
            catch (Exception ex_safe_log)
            {
                NonCriticalLog.EnqueueError("ExplorerV2", "PhotoshopLauncher", ex_safe_log);
            }
        }

        private static List<string> ResolveLaunchPaths(IEnumerable<string> filePaths)
        {
            var inputPaths = (filePaths ?? Array.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var resolvedPaths = new List<string>();
            foreach (var path in inputPaths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    var baseName = Path.GetFileNameWithoutExtension(path);
                    if (!string.IsNullOrWhiteSpace(dir) && !string.IsNullOrWhiteSpace(baseName) && Directory.Exists(dir))
                    {
                        var pattern = baseName + ".*";
                        var newest = Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly)
                            .Where(File.Exists)
                            .OrderByDescending(File.GetLastWriteTimeUtc)
                            .FirstOrDefault();

                        resolvedPaths.Add(string.IsNullOrWhiteSpace(newest) ? path : newest);
                    }
                    else
                    {
                        resolvedPaths.Add(path);
                    }
                }
                catch
                {
                    resolvedPaths.Add(path);
                }
            }

            return resolvedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void LaunchViaShell(IReadOnlyList<string> paths)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(paths[i]) { UseShellExecute = true });
                    PauseBetweenLaunches(i, paths.Count);
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "PhotoshopLauncher", ex_safe_log);
                }
            }
        }

        private static void LaunchViaPhotoshopExe(string exe, IReadOnlyList<string> paths)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exe, $"\"{paths[i]}\"") { UseShellExecute = false });
                    PauseBetweenLaunches(i, paths.Count);
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "PhotoshopLauncher", ex_safe_log);
                }
            }
        }

        private static void PauseBetweenLaunches(int index, int totalCount)
        {
            if (index < totalCount - 1)
            {
                Thread.Sleep(LaunchDelayMilliseconds);
            }
        }

        private static void ShowPhotoshopUnavailableMessage(string versionKey)
        {
            try
            {
                var displayName = KnownVersions
                    .FirstOrDefault(v => string.Equals(v.Key, versionKey, StringComparison.OrdinalIgnoreCase))
                    .DisplayName;

                var message = string.IsNullOrWhiteSpace(displayName)
                    ? "Photoshop was not found on this computer."
                    : displayName + " was not found on this computer.";

                System.Windows.MessageBox.Show(
                    message,
                    "SCHL App",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            catch
            {
            }
        }
    }
}
