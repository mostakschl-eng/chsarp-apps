using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.Views.ExplorerV2.Models;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    public sealed class WorkTypeDropContext
    {
        public string Name { get; init; } = string.Empty;
        public bool IsProduction { get; init; }
        public bool IsQc { get; init; }
        public bool IsQc1 { get; init; }
        public bool IsQcAc { get; init; }
        public bool IsTestFile { get; init; }
        public bool IsAdditional { get; init; }
        public bool IsShared { get; init; }
        public bool IsTranning { get; init; }
    }

    public class ExplorerV2DragDropService
    {
        public int GetMaxFilesPerUserOrDefault()
        {
            try
            {
                var cfg = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    as Microsoft.Extensions.Configuration.IConfiguration;

                var raw = (cfg?["TrackingSettings:MaxFilesPerUser"] ?? string.Empty).Trim();
                if (int.TryParse(raw, out var v) && v > 0)
                {
                    return v;
                }

                return 5;
            }
            catch
            {
                return 5;
            }
        }

        public bool RequiresMaxFilesLimit(WorkTypeDropContext wt)
        {
            try
            {
                return wt.IsProduction || wt.IsTestFile || wt.IsAdditional || wt.IsShared || wt.IsTranning;
            }
            catch
            {
                return false;
            }
        }

        public bool RequiresBaseDirForDrop(WorkTypeDropContext wt)
        {
            try
            {
                return wt.IsProduction || wt.IsQc || wt.IsQcAc || wt.IsTestFile || wt.IsAdditional || wt.IsShared || wt.IsTranning;
            }
            catch
            {
                return true;
            }
        }

        public bool AreAllDroppedPathsUnderBaseDir(string baseDir, IEnumerable<string> paths)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    return true;
                }

                var allUnderActive = true;
                foreach (var p in paths)
                {
                    var path = (p ?? string.Empty).Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    var dir = string.Empty;
                    try
                    {
                        dir = File.Exists(path)
                            ? (Path.GetDirectoryName(path) ?? string.Empty)
                            : (Directory.Exists(path) ? path : string.Empty);
                    }
                    catch
                    {
                        dir = string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(dir) || !FileOperationHelper.IsSameOrUnderPath(baseDir, dir))
                    {
                        allUnderActive = false;
                        break;
                    }
                }

                return allUnderActive;
            }
            catch
            {
                return true;
            }
        }

        public static string GetSafeUserNameForDrop(string appUser)
        {
            try
            {
                return FileOperationHelper.EnsureDirectorySafe(string.IsNullOrWhiteSpace(appUser) ? "_global" : appUser);
            }
            catch
            {
                return "_global";
            }
        }

        public static string GetSafeRealNameForDrop(string? displayName, int maxLength = 24)
        {
            try
            {
                var raw = (displayName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(raw)) return "_global";

                // Common format: "e0070 - Real Name". We want only the "Real Name" part.
                var dash = raw.IndexOf('-');
                if (dash >= 0 && dash + 1 < raw.Length)
                {
                    var right = raw.Substring(dash + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(right)) raw = right;
                }

                // Collapse whitespace
                raw = string.Join(" ", raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                var safe = FileOperationHelper.EnsureDirectorySafe(raw);
                if (string.IsNullOrWhiteSpace(safe)) safe = "_global";

                // Keep folder names short (prevents super long nested paths)
                if (maxLength > 0 && safe.Length > maxLength)
                {
                    safe = safe.Substring(0, maxLength).Trim();
                }

                return string.IsNullOrWhiteSpace(safe) ? "_global" : safe;
            }
            catch
            {
                return "_global";
            }
        }

        public List<SelectedFileRow> BuildDropRows(
            IEnumerable<string> paths,
            WorkTypeDropContext wt,
            string userNameSafe,
            HashSet<string> movedSourcePaths,
            HashSet<string> removeFromTiles)
        {
            var toAddRows = new List<SelectedFileRow>();
            try
            {
                var names = FileOperationHelper.GetExplorerV2WorkFolderNamesOrDefault();
                foreach (var p in paths)
                {
                    var path = (p ?? string.Empty).Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    var finalPath = path;
                    if (wt.IsProduction)
                    {
                        try
                        {
                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, names.Production, isCopy: false);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                                movedSourcePaths.Add(path);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (wt.IsTestFile)
                    {
                        try
                        {
                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, names.TfProduction, isCopy: false);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                                movedSourcePaths.Add(path);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (wt.IsAdditional)
                    {
                        try
                        {
                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, names.AdProduction, isCopy: false);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                                movedSourcePaths.Add(path);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (wt.IsShared)
                    {
                        try
                        {
                            var ext = Path.GetExtension(path) ?? string.Empty;
                            var nameNoExt = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                            var newName = nameNoExt + "." + userNameSafe + ext;

                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, names.SharedProduction, isCopy: true, destinationFileName: newName);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (wt.IsTranning)
                    {
                        try
                        {
                            var ext = Path.GetExtension(path) ?? string.Empty;
                            var nameNoExt = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                            var newName = nameNoExt + "." + userNameSafe + ext;

                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, names.TranningProduction, isCopy: true, destinationFileName: newName);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (wt.IsQcAc)
                    {
                        try
                        {
                            var qcFolderName = (names.QcAcPrefix + " " + userNameSafe).Trim();
                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, qcFolderName, isCopy: false);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                                movedSourcePaths.Add(path);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else if (wt.IsQc)
                    {
                        try
                        {
                            var prefix = wt.IsQc1 ? names.Qc1Prefix : names.Qc2Prefix;
                            var qcFolderName = (prefix + " " + userNameSafe).Trim();
                            var dest = FileOperationHelper.MoveFileToWorkFolder(path, qcFolderName, isCopy: false);
                            if (!string.IsNullOrWhiteSpace(dest))
                            {
                                finalPath = dest;
                                movedSourcePaths.Add(path);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else
                    {
                        removeFromTiles.Add(path);
                    }

                    toAddRows.Add(new SelectedFileRow
                    {
                        Serial = 0,
                        FullPath = finalPath,
                        FileName = Path.GetFileName(finalPath),
                        DisplayFileName = ToShortFileName(Path.GetFileName(finalPath))
                    });
                }
            }
            catch (Exception ex_safe_log)
            {
                NonCriticalLog.EnqueueError("ExplorerV2", "ExplorerV2DragDropService", ex_safe_log);
            }

            return toAddRows;
        }

        private static string ToShortFileName(string fileName)
        {
            try
            {
                var name = (fileName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return string.Empty;
                }

                if (name.Length <= 44)
                {
                    return name;
                }

                var first = name.Substring(0, 18);
                var last = name.Substring(name.Length - 18, 18);
                return first + "..." + last;
            }
            catch
            {
                return fileName ?? string.Empty;
            }
        }
    }
}
