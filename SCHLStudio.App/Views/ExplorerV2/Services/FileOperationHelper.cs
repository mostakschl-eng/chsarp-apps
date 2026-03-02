using SCHLStudio.App.ViewModels.Windows;
using SCHLStudio.App.Services.Diagnostics;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal static class FileOperationHelper
    {
        internal static bool IsSameOrUnderPath(string? rootPath, string? candidatePath)
        {
            try
            {
                var root = (rootPath ?? string.Empty).Trim();
                var candidate = (candidatePath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
                {
                    return false;
                }

                root = Path.GetFullPath(root);
                candidate = Path.GetFullPath(candidate);

                var sep = Path.DirectorySeparatorChar;
                if (!root.EndsWith(sep))
                {
                    root += sep;
                }

                if (!candidate.EndsWith(sep))
                {
                    candidate += sep;
                }

                return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        internal static string GetAppCurrentUser(FrameworkElement view)
        {
            try
            {
                var win = Window.GetWindow(view);
                return ((win?.DataContext as AppShellContext)?.CurrentUser ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string GetAppCurrentRole(FrameworkElement view)
        {
            try
            {
                var win = Window.GetWindow(view);
                return ((win?.DataContext as AppShellContext)?.CurrentRole ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string EnsureDirectorySafe(string name)
        {
            try
            {
                var n = (name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(n))
                {
                    return "Unknown";
                }

                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    n = n.Replace(c, '_');
                }

                return string.IsNullOrWhiteSpace(n) ? "Unknown" : n;
            }
            catch
            {
                return "Unknown";
            }
        }

        internal static string GetUniqueDestinationPath(string destinationPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    return destinationPath;
                }

                if (!File.Exists(destinationPath) && !Directory.Exists(destinationPath))
                {
                    return destinationPath;
                }

                var dir = Path.GetDirectoryName(destinationPath) ?? string.Empty;
                var name = Path.GetFileNameWithoutExtension(destinationPath) ?? string.Empty;
                var ext = Path.GetExtension(destinationPath) ?? string.Empty;

                for (var i = 1; i < 5000; i++)
                {
                    var candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                    if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return destinationPath;
            }
            catch
            {
                return destinationPath;
            }
        }

        internal static string? MoveFileToWorkFolder(string filePath, string folderName, bool isCopy, string? destinationFileName)
        {
            try
            {
                var path = (filePath ?? string.Empty).Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                var srcDir = Path.GetDirectoryName(path) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(srcDir))
                {
                    return null;
                }

                var safeFolderName = (folderName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(safeFolderName))
                {
                    return null;
                }

                var targetDir = Path.Combine(srcDir, safeFolderName);
                Directory.CreateDirectory(targetDir);

                var name = (destinationFileName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = Path.GetFileName(path);
                }

                var dest = Path.Combine(targetDir, name);

                if (isCopy)
                {
                    File.Copy(path, dest, overwrite: true);
                }
                else
                {
                    File.Move(path, dest, overwrite: true);
                }

                return File.Exists(dest) ? dest : null;
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "MoveFileToWorkFolder",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["filePath"] = filePath,
                            ["folderName"] = folderName,
                            ["isCopy"] = isCopy.ToString(),
                            ["destinationFileName"] = destinationFileName
                        });
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "FileOperationHelper", ex_safe_log);
                }

                return null;
            }
        }

        internal static string? MoveFileToWorkFolder(string filePath, string folderName, bool isCopy)
        {
            try
            {
                return MoveFileToWorkFolder(filePath, folderName, isCopy, null);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "MoveFileToWorkFolder.Wrapper",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["filePath"] = filePath,
                            ["folderName"] = folderName,
                            ["isCopy"] = isCopy.ToString()
                        });
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "FileOperationHelper", ex_safe_log);
                }
                return null;
            }
        }

        private static string GetExplorerV2WorkFolderConfigValueOrDefault(string configKey, string fallback)
        {
            try
            {
                var cfg = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    as Microsoft.Extensions.Configuration.IConfiguration;

                return GetExplorerV2WorkFolderConfigValueOrDefault(cfg, configKey, fallback);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "GetExplorerV2WorkFolderConfigValueOrDefault",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["configKey"] = configKey,
                            ["fallback"] = fallback
                        });
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "FileOperationHelper", ex_safe_log);
                }
                return fallback;
            }
        }

        private static string GetExplorerV2WorkFolderConfigValueOrDefault(Microsoft.Extensions.Configuration.IConfiguration? cfg, string configKey, string fallback)
        {
            try
            {
                var v = (cfg?[configKey] ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(v) ? fallback : v;
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "GetExplorerV2WorkFolderConfigValueOrDefault",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["configKey"] = configKey,
                            ["fallback"] = fallback
                        });
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "FileOperationHelper", ex_safe_log);
                }
                return fallback;
            }
        }

        internal sealed class ExplorerV2WorkFolderNames
        {
            public string Production { get; init; } = "Production";
            public string TfProduction { get; init; } = "TF Production";
            public string AdProduction { get; init; } = "AD Production";
            public string SharedProduction { get; init; } = "Shared Production";
            public string TranningProduction { get; init; } = "Tranning Production";

            public string QcAcPrefix { get; init; } = "QC AC";
            public string Qc1Prefix { get; init; } = "QC1";
            public string Qc2Prefix { get; init; } = "QC2";
        }

        internal static ExplorerV2WorkFolderNames GetExplorerV2WorkFolderNamesOrDefault()
        {
            try
            {
                var cfg = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    as Microsoft.Extensions.Configuration.IConfiguration;

                return new ExplorerV2WorkFolderNames
                {
                    Production = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:Production", "Production"),
                    TfProduction = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:TfProduction", "TF Production"),
                    AdProduction = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:AdProduction", "AD Production"),
                    SharedProduction = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:SharedProduction", "Shared Production"),
                    TranningProduction = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:TranningProduction", "Tranning Production"),

                    QcAcPrefix = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:QcAcPrefix", "QC AC"),
                    Qc1Prefix = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:Qc1Prefix", "QC1"),
                    Qc2Prefix = GetExplorerV2WorkFolderConfigValueOrDefault(cfg, "ExplorerV2:WorkFolders:Qc2Prefix", "QC2")
                };
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "ExplorerV2",
                        operation: "GetExplorerV2WorkFolderNamesOrDefault",
                        ex: ex);
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "FileOperationHelper", ex_safe_log);
                }
                return new ExplorerV2WorkFolderNames();
            }
        }
    }
}
