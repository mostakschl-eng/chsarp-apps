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
        private static readonly TimeSpan DoneRootCacheLifetime = TimeSpan.FromSeconds(15);

        private static readonly HashSet<string> AllowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".psd", ".psb"
        };

        private readonly object _doneRootCacheLock = new object();
        private readonly Dictionary<string, Dictionary<FilesViewMode, DoneRootCacheEntry>> _doneRootCache = new Dictionary<string, Dictionary<FilesViewMode, DoneRootCacheEntry>>(StringComparer.OrdinalIgnoreCase);

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

        private static string GetNearestDisplayParent(string[] parts)
        {
            if (parts == null || parts.Length < 2)
            {
                return string.Empty;
            }

            for (var index = parts.Length - 2; index >= 0; index--)
            {
                var candidate = (parts[index] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (BaseIgnoreFolderNames.Contains(candidate))
                {
                    continue;
                }

                if (candidate.StartsWith("QC1 ", StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith("QC2 ", StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith("QC AC ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate;
            }

            return (parts[parts.Length - 2] ?? string.Empty).Trim();
        }

        private sealed class DoneRootCacheEntry
        {
            public DateTime CachedAtUtc { get; init; }
            public IReadOnlyList<string> Roots { get; init; } = Array.Empty<string>();
        }

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

        public void InvalidateDoneRootCache(string? baseDirectoryPath = null)
        {
            lock (_doneRootCacheLock)
            {
                var baseDir = (baseDirectoryPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    _doneRootCache.Clear();
                    return;
                }

                _doneRootCache.Remove(baseDir);
            }
        }

        private IReadOnlyList<string> GetDoneRoots(string baseDir, FilesViewMode mode, HashSet<string> targetDoneFolderNames, System.Threading.CancellationToken cancellationToken)
        {
            if (TryGetCachedDoneRoots(baseDir, mode, out var cachedRoots))
            {
                return cachedRoots;
            }

            var discoveredRoots = DiscoverDoneRoots(baseDir, targetDoneFolderNames, cancellationToken);
            CacheDoneRoots(baseDir, mode, discoveredRoots);
            return discoveredRoots;
        }

        private bool TryGetCachedDoneRoots(string baseDir, FilesViewMode mode, out IReadOnlyList<string> roots)
        {
            roots = Array.Empty<string>();

            DoneRootCacheEntry? entry = null;
            lock (_doneRootCacheLock)
            {
                if (_doneRootCache.TryGetValue(baseDir, out var byMode)
                    && byMode.TryGetValue(mode, out var cachedEntry))
                {
                    entry = cachedEntry;
                }
            }

            if (entry is null)
            {
                return false;
            }

            if ((DateTime.UtcNow - entry.CachedAtUtc) > DoneRootCacheLifetime)
            {
                InvalidateDoneRootCache(baseDir);
                return false;
            }

            var currentRoots = entry.Roots ?? Array.Empty<string>();
            if (currentRoots.Count == 0)
            {
                roots = currentRoots;
                return true;
            }

            foreach (var root in currentRoots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    InvalidateDoneRootCache(baseDir);
                    return false;
                }
            }

            roots = currentRoots;
            return true;
        }

        private void CacheDoneRoots(string baseDir, FilesViewMode mode, IReadOnlyList<string> roots)
        {
            var safeRoots = (roots ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            lock (_doneRootCacheLock)
            {
                if (!_doneRootCache.TryGetValue(baseDir, out var byMode))
                {
                    byMode = new Dictionary<FilesViewMode, DoneRootCacheEntry>();
                    _doneRootCache[baseDir] = byMode;
                }

                byMode[mode] = new DoneRootCacheEntry
                {
                    CachedAtUtc = DateTime.UtcNow,
                    Roots = safeRoots
                };
            }
        }

        private static IReadOnlyList<string> DiscoverDoneRoots(string baseDir, HashSet<string> targetDoneFolderNames, System.Threading.CancellationToken cancellationToken)
        {
            var roots = new List<string>();
            var pending = new Stack<string>();
            pending.Push(baseDir);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dirPath = pending.Pop();
                var dirName = GetDirectoryNameSafe(dirPath);
                var isDoneRoot = !string.IsNullOrWhiteSpace(dirName)
                    && targetDoneFolderNames.Contains(dirName);

                if (isDoneRoot)
                {
                    roots.Add(dirPath);
                    continue;
                }

                try
                {
                    foreach (var subDir in Directory.EnumerateDirectories(dirPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pending.Push(subDir);
                    }
                }
                catch (Exception ex_safe_log)
                {
                    NonCriticalLog.EnqueueError("ExplorerV2", "FileIndexService", ex_safe_log);
                }
            }

            return roots;
        }

        private static string GetDirectoryNameSafe(string dirPath)
        {
            try
            {
                var trimmed = (dirPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dirName = Path.GetFileName(trimmed) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(dirName))
                {
                    return dirName;
                }

                return new DirectoryInfo(dirPath).Name;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildDisplayFolderName(string baseDir, string dirPath, string dirName, FilesViewMode mode, bool isDoneView)
        {
            var displayFolderName = dirName;

            try
            {
                var trimmedDir = dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var relPathStr = Path.GetRelativePath(baseDir, trimmedDir);

                if (!string.IsNullOrWhiteSpace(relPathStr) && relPathStr != ".")
                {
                    var parts = relPathStr.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                    var groupedDoneMode = mode == FilesViewMode.ProductionDone
                        || mode == FilesViewMode.Qc1AllDone
                        || mode == FilesViewMode.Qc1Done
                        || mode == FilesViewMode.Qc2AllDone
                        || mode == FilesViewMode.Qc2Done;

                    if (isDoneView)
                    {
                        if (parts.Length > 1)
                        {
                            var parentName = GetNearestDisplayParent(parts);
                            if (groupedDoneMode)
                            {
                                if (parts.Length > 2 && !string.Equals(parts[0], parentName, StringComparison.OrdinalIgnoreCase))
                                {
                                    displayFolderName = $"{parts[0]} • {parentName} • {dirName}";
                                }
                                else
                                {
                                    displayFolderName = $"{parentName} • {dirName}";
                                }
                            }
                            else if (parts.Length > 2)
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

            return displayFolderName;
        }

        private IReadOnlyList<FileTileItem> BuildDoneTiles(string baseDir, FilesViewMode mode, HashSet<string> targetDoneFolderNames, System.Threading.CancellationToken cancellationToken)
        {
            var tiles = new List<FileTileItem>();
            var doneRoots = GetDoneRoots(baseDir, mode, targetDoneFolderNames, cancellationToken);

            foreach (var doneRoot in doneRoots)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dirName = GetDirectoryNameSafe(doneRoot);
                if (string.IsNullOrWhiteSpace(dirName))
                {
                    continue;
                }

                var displayFolderName = BuildDisplayFolderName(baseDir, doneRoot, dirName, mode, isDoneView: true);

                try
                {
                    foreach (var path in Directory.EnumerateFiles(doneRoot, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var ext = Path.GetExtension(path) ?? string.Empty;
                        if (!AllowedExts.Contains(ext))
                        {
                            continue;
                        }

                        var extNorm = string.IsNullOrWhiteSpace(ext) ? string.Empty : ext.Trim().ToLowerInvariant();
                        tiles.Add(new FileTileItem
                        {
                            FullPath = path,
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

                if (mode != FilesViewMode.Work && targetDoneFolderNames.Count > 0)
                {
                    return BuildDoneTiles(baseDir, mode, targetDoneFolderNames, cancellationToken);
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

                    var displayFolderName = BuildDisplayFolderName(baseDir, dirPath, dirName, mode, isDoneView: false);

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
