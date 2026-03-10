using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SCHLStudio.App.Services.Diagnostics
{
    internal static class AppDataLog
    {
        private static readonly object _ioLock = new();

        private static string ResolveLogFileName(string? area)
        {
            try
            {
                var a = (area ?? string.Empty).Trim();
                if (string.Equals(a, "ExplorerV2", StringComparison.OrdinalIgnoreCase))
                {
                    return "explorerv2.log";
                }

                if (string.Equals(a, "Tracker", StringComparison.OrdinalIgnoreCase))
                {
                    return "tracker.log";
                }

                if (string.Equals(a, "Update", StringComparison.OrdinalIgnoreCase))
                {
                    return "update.log";
                }

                if (string.Equals(a, "App", StringComparison.OrdinalIgnoreCase))
                {
                    return "app.log";
                }

                return "general.log";
            }
            catch
            {
                return "general.log";
            }
        }

        private static string ResolveEventFileName(string? area)
        {
            try
            {
                var a = (area ?? string.Empty).Trim();
                if (string.Equals(a, "Tracker", StringComparison.OrdinalIgnoreCase))
                {
                    return "tracker.events.jsonl";
                }

                return "events.jsonl";
            }
            catch
            {
                return "events.jsonl";
            }
        }

        private static string ResolveRoot(string? userName)
        {
            var fallbackUser = Configuration.AppConfig.StorageUserSegment;
            var resolved = string.IsNullOrWhiteSpace(userName) ? fallbackUser : userName;

            // Keep diagnostics folder naming aligned with the storage segment used by app data/tracker.
            var currentAppUser = (Configuration.AppConfig.CurrentAppUser ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(currentAppUser)
                && string.Equals((resolved ?? string.Empty).Trim(), currentAppUser, StringComparison.OrdinalIgnoreCase))
            {
                resolved = fallbackUser;
            }

            if (string.Equals(resolved?.Trim(), "UnknownUser", StringComparison.OrdinalIgnoreCase))
            {
                resolved = "_global";
            }

            var user = EnsureDirectorySafe(resolved);
            if (string.IsNullOrWhiteSpace(user))
            {
                user = "_global";
            }

            if (string.Equals(user, "UnknownUser", StringComparison.OrdinalIgnoreCase))
            {
                user = "_global";
            }

            var date = DateTime.Now.ToString("yyyy-MM-dd");
            // Keep logs local under the same user/date tree used by local tracker queue.
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SCHLStudio",
                user,
                date,
                "logs");
            Directory.CreateDirectory(root);
            return root;
        }

        internal static void LogEvent(string area, string operation, string level, IReadOnlyDictionary<string, string?>? data = null, string? userName = null)
        {
            try
            {
                var root = ResolveRoot(userName);
                var file = Path.Combine(root, ResolveEventFileName(area));

                var payload = new Dictionary<string, object?>
                {
                    ["ts"] = DateTime.Now.ToString("O"),
                    ["area"] = area,
                    ["operation"] = operation,
                    ["level"] = level
                };

                if (data is not null && data.Count > 0)
                {
                    payload["data"] = data;
                }

                var line = JsonSerializer.Serialize(payload);
                lock (_ioLock)
                {
                    File.AppendAllText(file, line + Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        internal static void LogEventError(string area, string operation, Exception ex, IReadOnlyDictionary<string, string?>? data = null, string? userName = null)
        {
            try
            {
                var combined = new Dictionary<string, string?>
                {
                    ["exceptionType"] = ex.GetType().Name,
                    ["exceptionMessage"] = ex.Message
                };

                if (data is not null)
                {
                    foreach (var kv in data.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
                    {
                        combined[kv.Key] = kv.Value;
                    }
                }

                LogEvent(area, operation, "error", combined, userName);
            }
            catch
            {
            }
        }

        internal static void LogError(string area, string operation, Exception ex, IReadOnlyDictionary<string, string?>? data = null, string? userName = null)
        {
            try
            {
                var root = ResolveRoot(userName);

                var file = Path.Combine(root, ResolveLogFileName(area));

                var lines = new List<string>
                {
                    "[" + DateTime.Now.ToString("O") + "] " + (area ?? string.Empty) + "::" + (operation ?? string.Empty),
                    ex.ToString()
                };

                try
                {
                    if (data is not null && data.Count > 0)
                    {
                        foreach (var kv in data.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
                        {
                            lines.Add("  " + kv.Key + ": " + (kv.Value ?? string.Empty));
                        }
                    }
                }
                catch
                {
                }

                lines.Add(string.Empty);

                lock (_ioLock)
                {
                    File.AppendAllLines(file, lines);
                }

                try
                {
                    LogEventError(area ?? string.Empty, operation ?? string.Empty, ex, data, userName);
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        internal static string EnsureDirectorySafe(string? name)
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
    }
}
