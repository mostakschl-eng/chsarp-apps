using System;
using System.Collections.Generic;
using System.IO;
using SCHLStudio.App.Services.Diagnostics;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal sealed class DoneMoveService
    {
        private const string RawFolderName = "Raw";

        public void MoveToDone(IEnumerable<string> filePaths, string? workType)
        {
            try
            {
                var normalized = (workType ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    return;
                }

                if (string.Equals(normalized, "QC 1", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToQcDone(filePaths, isQc1: true);
                    return;
                }

                if (string.Equals(normalized, "QC 2", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToQcDone(filePaths, isQc1: false);
                    return;
                }

                if (string.Equals(normalized, "Production", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToDoneFolder(filePaths, "Production", "Production Done");
                    return;
                }

                if (string.Equals(normalized, "Shared", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToDoneFolder(filePaths, "Shared Production", "Shared Production Done");
                    return;
                }

                if (string.Equals(normalized, "Test File", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToDoneFolder(filePaths, "TF Production", "TF Production Done");
                    return;
                }

                if (string.Equals(normalized, "Additional", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToDoneFolder(filePaths, "AD Production", "AD Production Done");
                    return;
                }

                if (string.Equals(normalized, "Tranning", StringComparison.OrdinalIgnoreCase))
                {
                    MoveToDoneFolder(filePaths, "Tranning Production", "Tranning Production Done");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToDone), ex, new Dictionary<string, string?>
                {
                    ["workType"] = workType
                });
            }
        }

        public void MoveToReadyToUpload(string baseDir, IEnumerable<string> filePaths)
        {
            try
            {
                var root = (baseDir ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    return;
                }

                var readyDir = Path.Combine(root, "Ready To Upload");
                Directory.CreateDirectory(readyDir);

                foreach (var srcRaw in filePaths ?? Array.Empty<string>())
                {
                    try
                    {
                        var src = (srcRaw ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                        {
                            continue;
                        }

                        var dest = Path.Combine(readyDir, Path.GetFileName(src));
                        File.Move(src, dest, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            AppDataLog.LogError(
                                area: "ExplorerV2",
                                operation: "MoveToReadyToUpload",
                                ex: ex,
                                data: new Dictionary<string, string?>
                                {
                                    ["baseDir"] = baseDir,
                                    ["src"] = srcRaw
                                });
                        }
                        catch (Exception ex_safe_log)
                        {
                            NonCriticalLog.EnqueueError("ExplorerV2", "DoneMoveService", ex_safe_log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToReadyToUpload), ex, new Dictionary<string, string?>
                {
                    ["baseDir"] = baseDir
                });
            }
        }

        public void MoveBackFromProductionToParent(IEnumerable<string> filePaths)
        {
            try
            {
                MoveBackToParentIfInFolder(
                    filePaths: filePaths,
                    folderNameToMatch: "Production",
                    operationName: "MoveBackFromProductionToParent",
                    extraData: null);
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveBackFromProductionToParent), ex);
            }
        }

        public void MoveBackFromWorkFoldersToParent(IEnumerable<string> filePaths, string? currentUser)
        {
            try
            {
                var names = FileOperationHelper.GetExplorerV2WorkFolderNamesOrDefault();

                var userSafe = string.Empty;
                try
                {
                    userSafe = FileOperationHelper.EnsureDirectorySafe((currentUser ?? string.Empty).Trim());
                }
                catch
                {
                    userSafe = string.Empty;
                }

                var userRealSafe = string.Empty;
                try
                {
                    userRealSafe = ExplorerV2DragDropService.GetSafeRealNameForDrop(currentUser);
                }
                catch
                {
                    userRealSafe = string.Empty;
                }

                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    (names.Production ?? string.Empty).Trim(),
                    (names.AdProduction ?? string.Empty).Trim(),
                    (names.TfProduction ?? string.Empty).Trim(),
                    (names.SharedProduction ?? string.Empty).Trim(),
                    (names.TranningProduction ?? string.Empty).Trim(),
                };

                if (!string.IsNullOrWhiteSpace(userSafe))
                {
                    folders.Add(((names.Qc1Prefix ?? string.Empty).Trim() + " " + userSafe).Trim());
                    folders.Add(((names.Qc2Prefix ?? string.Empty).Trim() + " " + userSafe).Trim());
                    folders.Add(((names.QcAcPrefix ?? string.Empty).Trim() + " " + userSafe).Trim());
                }

                if (!string.IsNullOrWhiteSpace(userRealSafe) && !string.Equals(userRealSafe, userSafe, StringComparison.OrdinalIgnoreCase))
                {
                    folders.Add(((names.Qc1Prefix ?? string.Empty).Trim() + " " + userRealSafe).Trim());
                    folders.Add(((names.Qc2Prefix ?? string.Empty).Trim() + " " + userRealSafe).Trim());
                    folders.Add(((names.QcAcPrefix ?? string.Empty).Trim() + " " + userRealSafe).Trim());
                }

                foreach (var srcRaw in filePaths ?? Array.Empty<string>())
                {
                    try
                    {
                        var src = (srcRaw ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                        {
                            continue;
                        }

                        var srcDir = Path.GetDirectoryName(src) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(srcDir))
                        {
                            continue;
                        }

                        var srcFolderName = string.Empty;
                        try
                        {
                            srcFolderName = (new DirectoryInfo(srcDir).Name ?? string.Empty).Trim();
                        }
                        catch
                        {
                            srcFolderName = string.Empty;
                        }

                        if (string.IsNullOrWhiteSpace(srcFolderName) || !folders.Contains(srcFolderName))
                        {
                            continue;
                        }

                        var parentDir = Directory.GetParent(srcDir)?.FullName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(parentDir))
                        {
                            continue;
                        }

                        var dest = Path.Combine(parentDir, Path.GetFileName(src));
                        File.Move(src, dest, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            AppDataLog.LogError(
                                area: "ExplorerV2",
                                operation: "MoveBackFromWorkFoldersToParent",
                                ex: ex,
                                data: new Dictionary<string, string?>
                                {
                                    ["src"] = srcRaw,
                                    ["currentUser"] = currentUser
                                });
                        }
                        catch (Exception ex_safe_log)
                        {
                            NonCriticalLog.EnqueueError("ExplorerV2", "DoneMoveService", ex_safe_log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveBackFromWorkFoldersToParent), ex, new Dictionary<string, string?>
                {
                    ["currentUser"] = currentUser
                });
            }
        }

        private void MoveBackToParentIfInFolder(
            IEnumerable<string> filePaths,
            string folderNameToMatch,
            string operationName,
            Dictionary<string, string?>? extraData)
        {
            try
            {
                var match = (folderNameToMatch ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(match))
                {
                    return;
                }

                foreach (var srcRaw in filePaths ?? Array.Empty<string>())
                {
                    try
                    {
                        var src = (srcRaw ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                        {
                            continue;
                        }

                        var srcDir = Path.GetDirectoryName(src) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(srcDir))
                        {
                            continue;
                        }

                        var parentDir = srcDir;
                        try
                        {
                            if (string.Equals(new DirectoryInfo(srcDir).Name, match, StringComparison.OrdinalIgnoreCase))
                            {
                                parentDir = Directory.GetParent(srcDir)?.FullName ?? srcDir;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogSuppressed(operationName + ".ResolveParent", ex, new Dictionary<string, string?>
                            {
                                ["srcDir"] = srcDir,
                                ["match"] = match
                            });
                            parentDir = srcDir;
                        }

                        if (string.IsNullOrWhiteSpace(parentDir))
                        {
                            continue;
                        }

                        var dest = Path.Combine(parentDir, Path.GetFileName(src));
                        File.Move(src, dest, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            var data = new Dictionary<string, string?>
                            {
                                ["src"] = srcRaw
                            };

                            if (extraData is not null)
                            {
                                foreach (var kvp in extraData)
                                {
                                    data[kvp.Key] = kvp.Value;
                                }
                            }

                            AppDataLog.LogError(
                                area: "ExplorerV2",
                                operation: operationName,
                                ex: ex,
                                data: data);
                        }
                        catch (Exception ex_safe_log)
                        {
                            NonCriticalLog.EnqueueError("ExplorerV2", "DoneMoveService", ex_safe_log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSuppressed(operationName, ex, extraData);
            }
        }

        public void MoveToQcDone(IEnumerable<string> filePaths, bool isQc1)
        {
            try
            {
                var qcPrefix = isQc1 ? "QC1" : "QC2";
                MoveToDoneFolderCore(
                    filePaths,
                    srcDir => ResolveQcDoneFolder(srcDir, qcPrefix),
                    operationName: nameof(MoveToQcDone),
                    operationData: new Dictionary<string, string?>
                    {
                        ["isQc1"] = isQc1.ToString()
                    });
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToQcDone), ex, new Dictionary<string, string?>
                {
                    ["isQc1"] = isQc1.ToString()
                });
            }
        }

        private (string doneDirName, string doneDirParent) ResolveQcDoneFolder(string srcDir, string qcPrefix)
        {
            var fallbackDoneDirName = string.Equals(qcPrefix, "QC1", StringComparison.OrdinalIgnoreCase)
                ? "QC1 Production Done"
                : "QC2 Production Done";

            var doneDirName = fallbackDoneDirName;
            var doneDirParent = srcDir;

            try
            {
                var scan = new DirectoryInfo(srcDir);
                var depth = 0;
                const int maxDepth = 40;
                while (scan != null)
                {
                    if (depth++ >= maxDepth)
                    {
                        break;
                    }

                    var name = (scan.Name ?? string.Empty).Trim();

                    if (!string.IsNullOrWhiteSpace(name) && name.StartsWith(qcPrefix + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        scan = scan.Parent;
                        continue;
                    }

                    if (string.Equals(name, "Production Done", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " Production Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "Shared Done", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "Shared Production Done", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " Shared Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "TF Done", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "TF Production Done", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " TF Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "AD Done", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "AD Production Done", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " AD Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "Shared Production", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " Shared Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "TF Production", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " TF Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "AD Production", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " AD Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "Tranning Done", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "Tranning Production Done", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " Tranning Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "Tranning Production", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " Tranning Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    if (string.Equals(name, "Production", StringComparison.OrdinalIgnoreCase))
                    {
                        doneDirName = qcPrefix + " Production Done";
                        doneDirParent = scan.FullName;
                        break;
                    }

                    scan = scan.Parent;
                }
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(ResolveQcDoneFolder), ex, new Dictionary<string, string?>
                {
                    ["srcDir"] = srcDir,
                    ["qcPrefix"] = qcPrefix
                });
                doneDirName = fallbackDoneDirName;
                doneDirParent = srcDir;
            }

            return (doneDirName, doneDirParent);
        }

        public void MoveToProductionDone(IEnumerable<string> filePaths)
        {
            try
            {
                MoveToDone(filePaths, "Production");
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToProductionDone), ex);
            }
        }

        private void MoveToDoneFolder(IEnumerable<string> filePaths, string productionFolderName, string doneFolderName)
        {
            try
            {
                MoveToDoneFolderCore(
                    filePaths,
                    srcDir => ResolveStandardDoneFolder(srcDir, productionFolderName, doneFolderName),
                    operationName: nameof(MoveToDoneFolder),
                    operationData: new Dictionary<string, string?>
                    {
                        ["productionFolderName"] = productionFolderName,
                        ["doneFolderName"] = doneFolderName
                    });
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToDoneFolder), ex, new Dictionary<string, string?>
                {
                    ["productionFolderName"] = productionFolderName,
                    ["doneFolderName"] = doneFolderName
                });
            }
        }

        public void MoveToSharedDone(IEnumerable<string> filePaths)
        {
            try
            {
                MoveToDone(filePaths, "Shared");
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToSharedDone), ex);
            }
        }

        public void MoveToTfDone(IEnumerable<string> filePaths)
        {
            try
            {
                MoveToDone(filePaths, "Test File");
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToTfDone), ex);
            }
        }

        public void MoveToAdDone(IEnumerable<string> filePaths)
        {
            try
            {
                MoveToDone(filePaths, "Additional");
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToAdDone), ex);
            }
        }

        public void MoveToDoneByWorkType(IEnumerable<string> filePaths, string? workType)
        {
            try
            {
                MoveToDone(filePaths, workType);
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(MoveToDoneByWorkType), ex, new Dictionary<string, string?>
                {
                    ["workType"] = workType
                });
            }
        }

        private void MoveToDoneFolderCore(
            IEnumerable<string> filePaths,
            Func<string, (string doneDirName, string doneDirParent)> resolveDoneFolder,
            string operationName,
            IReadOnlyDictionary<string, string?>? operationData = null)
        {
            foreach (var srcRaw in filePaths ?? Array.Empty<string>())
            {
                try
                {
                    var src = (srcRaw ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                    {
                        continue;
                    }

                    var srcDir = Path.GetDirectoryName(src) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(srcDir))
                    {
                        continue;
                    }

                    var basename = Path.GetFileNameWithoutExtension(src) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(basename))
                    {
                        continue;
                    }

                    var candidates = GetCandidateFiles(srcDir, basename);
                    if (candidates.Count == 0)
                    {
                        continue;
                    }

                    candidates.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                    var winner = candidates[0];
                    var supporters = candidates.Count > 1 ? candidates.GetRange(1, candidates.Count - 1) : new List<string>();

                    var done = resolveDoneFolder(srcDir);
                    var doneDir = Path.Combine(done.doneDirParent, done.doneDirName);
                    Directory.CreateDirectory(doneDir);

                    var dest = Path.Combine(doneDir, Path.GetFileName(winner));

                    if (File.Exists(winner))
                    {
                        File.Move(winner, dest, overwrite: true);
                    }

                    if (supporters.Count > 0)
                    {
                        var rawDir = Path.Combine(srcDir, RawFolderName);
                        Directory.CreateDirectory(rawDir);

                        foreach (var sup in supporters)
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(sup) || !File.Exists(sup))
                                {
                                    continue;
                                }

                                var supDest = Path.Combine(rawDir, Path.GetFileName(sup));
                                File.Move(sup, supDest, overwrite: true);
                            }
                            catch (Exception ex)
                            {
                                LogSuppressed(operationName + ".MoveSupporter", ex, new Dictionary<string, string?>
                                {
                                    ["supporter"] = sup,
                                    ["srcDir"] = srcDir
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var data = new Dictionary<string, string?>
                    {
                        ["src"] = srcRaw
                    };

                    if (operationData is not null)
                    {
                        foreach (var kv in operationData)
                        {
                            data[kv.Key] = kv.Value;
                        }
                    }

                    LogSuppressed(operationName, ex, data);
                }
            }
        }

        private (string doneDirName, string doneDirParent) ResolveStandardDoneFolder(string srcDir, string sourceFolderName, string doneFolderName)
        {
            var parentDir = srcDir;

            try
            {
                if (string.Equals(new DirectoryInfo(srcDir).Name, sourceFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    parentDir = Directory.GetParent(srcDir)?.FullName ?? srcDir;
                }
            }
            catch (Exception ex)
            {
                LogSuppressed("ResolveStandardDoneFolder.ResolveParent", ex, new Dictionary<string, string?>
                {
                    ["srcDir"] = srcDir,
                    ["sourceFolderName"] = sourceFolderName
                });
                parentDir = srcDir;
            }

            return (doneFolderName, parentDir);
        }

        private static List<string> GetCandidateFiles(string workingDir, string basename)
        {
            try
            {
                var pattern = (basename ?? string.Empty).Trim() + ".*";
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return new List<string>();
                }

                return Directory.EnumerateFiles(workingDir, pattern, SearchOption.TopDirectoryOnly)
                    .Where(File.Exists)
                    .ToList();
            }
            catch (Exception ex)
            {
                LogSuppressed(nameof(GetCandidateFiles), ex, new Dictionary<string, string?>
                {
                    ["workingDir"] = workingDir,
                    ["basename"] = basename
                });
                return new List<string>();
            }
        }

        private static void LogSuppressed(string operation, Exception ex, IReadOnlyDictionary<string, string?>? data = null)
        {
            try
            {
                AppDataLog.LogError(
                    area: "ExplorerV2",
                    operation: "DoneMoveService." + (operation ?? string.Empty),
                    ex: ex,
                    data: data);
            }
            catch (Exception ex_safe_log)
            {
                NonCriticalLog.EnqueueError("ExplorerV2", "DoneMoveService", ex_safe_log);
            }
        }

    }
}
