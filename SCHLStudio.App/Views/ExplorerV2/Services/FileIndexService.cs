using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.Views.ExplorerV2.Models;

namespace SCHLStudio.App.Views.ExplorerV2.Services
{
    internal sealed class FileIndexService
    {
        private static readonly HashSet<string> AllowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".psd", ".psb"
        };

        private static readonly HashSet<string> BaseIgnoreFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Production",
            "Production Done",
            "Shared Production Done",
            "Tranning Production Done",
            "TF Production Done",
            "AD Production Done",
            "QC Done",
            "QC1 Done",
            "QC2 Done",
            "QC1 Production Done",
            "QC1 Shared Done",
            "QC1 TF Done",
            "QC1 AD Done",
            "QC2 Production Done",
            "QC2 Shared Done",
            "QC2 TF Done",
            "QC2 AD Done",
            "QC AC Done",
            "Ready To Upload",
            "Backup",
            "Raw",
            "Walk Out",
            "Supporting",
            "Shared Production",
            "Tranning Production",
            "Shared Done",
            "TF Production",
            "TF Done",
            "AD Production",
            "AD Done",
            "Sample",
            "Reference"
        };

        internal enum FilesViewMode
        {
            Work,
            ProductionDone,
            SharedDone,
            QcAcDone,
            TfDone,
            AdDone,
            AllDone,
            Qc1AllDone,
            Qc1ProductionDone,
            Qc1SharedDone,
            Qc1TfDone,
            Qc1AdDone,
            Qc2AllDone,
            Qc2ProductionDone,
            Qc2SharedDone,
            Qc2TfDone,
            Qc2AdDone,
            Qc1Done,
            Qc2Done
        }

        public IReadOnlyList<FileTileItem> BuildTiles(string baseDirectoryPath, FilesViewMode mode, string? currentUser, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var allowedExts = AllowedExts;
                var ignoreFolderNames = new HashSet<string>(BaseIgnoreFolderNames, StringComparer.OrdinalIgnoreCase);

                var userTrim = (currentUser ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(userTrim))
                {
                    ignoreFolderNames.Add(userTrim);
                }

                var targetDoneFolderNames = mode switch
                {
                    FilesViewMode.ProductionDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Production Done",
                        "Shared Production Done",
                        "Tranning Production Done",
                        "TF Production Done",
                        "AD Production Done"
                    },
                    FilesViewMode.SharedDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Shared Done" },
                    FilesViewMode.QcAcDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC AC Done" },
                    FilesViewMode.TfDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TF Done" },
                    FilesViewMode.AdDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AD Done" },
                    FilesViewMode.Qc1AllDone or FilesViewMode.Qc1Done => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "QC1 Production Done",
                        "QC1 Shared Done",
                        "QC1 TF Done",
                        "QC1 AD Done",
                        "QC1 Tranning Done"
                    },
                    FilesViewMode.Qc1ProductionDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC1 Production Done" },
                    FilesViewMode.Qc1SharedDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC1 Shared Done" },
                    FilesViewMode.Qc1TfDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC1 TF Done" },
                    FilesViewMode.Qc1AdDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC1 AD Done" },
                    FilesViewMode.Qc2AllDone or FilesViewMode.Qc2Done => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "QC2 Production Done",
                        "QC2 Shared Done",
                        "QC2 TF Done",
                        "QC2 AD Done",
                        "QC2 Tranning Done"
                    },
                    FilesViewMode.Qc2ProductionDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC2 Production Done" },
                    FilesViewMode.Qc2SharedDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC2 Shared Done" },
                    FilesViewMode.Qc2TfDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC2 TF Done" },
                    FilesViewMode.Qc2AdDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "QC2 AD Done" },
                    FilesViewMode.AllDone => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Production Done",
                        "QC1 Production Done",
                        "QC1 Shared Done",
                        "QC1 TF Done",
                        "QC1 AD Done",
                        "QC1 Tranning Done",
                        "QC2 Production Done",
                        "QC2 Shared Done",
                        "QC2 TF Done",
                        "QC2 AD Done",
                        "QC2 Tranning Done",
                        "QC AC Done",
                        "Shared Production Done",
                        "Tranning Production Done",
                        "TF Production Done",
                        "AD Production Done"
                    },
                    _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                };

                var baseDir = (baseDirectoryPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    return Array.Empty<FileTileItem>();
                }

                var tiles = new List<FileTileItem>();
                var pending = new Stack<(string Dir, bool InDone)>();
                pending.Push((baseDir, false));

                var isDoneView = mode != FilesViewMode.Work;
                var includeDoneSubfolders = false;

                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (dir, inDone) = pending.Pop();
                    var dirPath = dir ?? string.Empty;

                    var dirName = string.Empty;
                    try
                    {
                        var trimmed = dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        dirName = Path.GetFileName(trimmed) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(dirName))
                        {
                            dirName = new DirectoryInfo(dirPath).Name;
                        }
                    }
                    catch
                    {
                        dirName = string.Empty;
                    }

                    var isThisDoneRoot = targetDoneFolderNames.Count > 0
                        && !string.IsNullOrWhiteSpace(dirName)
                        && targetDoneFolderNames.Contains(dirName);

                    if (mode == FilesViewMode.Work)
                    {
                        var isIgnoreName = !string.IsNullOrWhiteSpace(dirName) && ignoreFolderNames.Contains(dirName);
                        var isQcAcUserFolder = !string.IsNullOrWhiteSpace(dirName)
                            && dirName.StartsWith("QC AC ", StringComparison.OrdinalIgnoreCase);

                        if ((isIgnoreName || isQcAcUserFolder) && !string.Equals(dir, baseDir, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var nextInDone = inDone || isThisDoneRoot;

                    try
                    {
                        // Done/QC Done views should only show files in the done folder root itself.
                        // Do not traverse into subfolders under a done root (e.g. "Production Done\\QC John").
                        if (!(isDoneView && isThisDoneRoot && !includeDoneSubfolders))
                        {
                            foreach (var sub in Directory.EnumerateDirectories(dirPath))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                pending.Push((sub, nextInDone));
                            }
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        NonCriticalLog.EnqueueError("ExplorerV2", "FileIndexService", ex_safe_log);
                    }

                    var shouldCollectFiles = mode == FilesViewMode.Work
                        ? true
                        : (isThisDoneRoot || (includeDoneSubfolders && inDone));
                    if (!shouldCollectFiles)
                    {
                        continue;
                    }

                    var displayFolderName = dirName;

                    try
                    {
                        var trimmedDir = dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var relPathStr = Path.GetRelativePath(baseDir, trimmedDir);
                        
                        if (!string.IsNullOrWhiteSpace(relPathStr) && relPathStr != ".")
                        {
                            var parts = relPathStr.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                            
                            if (isDoneView && (isThisDoneRoot || inDone))
                            {
                                // For done files, the last part is usually "Production Done", so we want the parent of that.
                                if (parts.Length > 1)
                                {
                                    var parentName = parts[parts.Length - 2];
                                    if (parts.Length > 2)
                                    {
                                        displayFolderName = $"{parts[0]} \u2022 {parentName}";
                                    }
                                    else
                                    {
                                        displayFolderName = parentName;
                                    }
                                }
                            }
                            else
                            {
                                // Normal work files
                                if (parts.Length > 1)
                                {
                                    displayFolderName = $"{parts[0]} \u2022 {parts[parts.Length - 1]}";
                                }
                                else if (parts.Length == 1)
                                {
                                    displayFolderName = parts[0];
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Fallback to exactly dirName
                    }

                    try
                    {
                        foreach (var p in Directory.EnumerateFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var ext = Path.GetExtension(p) ?? string.Empty;
                            if (!allowedExts.Contains(ext))
                            {
                                continue;
                            }

                            var extNorm = string.IsNullOrWhiteSpace(ext) ? string.Empty : ext.Trim().ToLowerInvariant();
                            tiles.Add(new FileTileItem
                            {
                                FullPath = p,
                                Extension = extNorm.TrimStart('.').ToUpperInvariant(),
                                ExtensionLower = extNorm,
                                FolderName = displayFolderName,
                                IsHeader = false
                            });
                        }
                    }
                    catch (Exception ex_safe_log)
                    {
                        NonCriticalLog.EnqueueError("ExplorerV2", "FileIndexService", ex_safe_log);
                    }
                }

                return tiles;
            }
            catch
            {
                return Array.Empty<FileTileItem>();
            }
        }
    }
}
