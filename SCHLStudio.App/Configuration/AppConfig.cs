using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace SCHLStudio.App.Configuration
{
    /// <summary>
    /// Centralized configuration constants for SCHL Time Tracker.
    /// Direct port of Python config.py - maintains exact same values for parity.
    /// </summary>
    public static class AppConfig
    {
        // ==================== FILE TRACKING ====================

        /// <summary>
        /// Minimum time (in seconds) a file must be tracked before it's considered valid.
        /// Files closed before this threshold are auto-skipped.
        /// </summary>
        public const int MIN_TRACKING_TIME_SECONDS = 10;

        /// <summary>
        /// Maximum number of files non-QC users can track simultaneously.
        /// </summary>
        public const int MAX_FILES_PER_USER = 5;

        /// <summary>
        /// Auto-pause timeout when no Photoshop documents are open (in seconds).
        /// </summary>
        public const int AUTO_PAUSE_NO_DOC_TIMEOUT = 30;

        /// <summary>
        /// File switch cooldown to prevent rapid re-processing (in milliseconds).
        /// </summary>
        public const int FILE_SWITCH_COOLDOWN_MS = 2000;

        /// <summary>
        /// If true, the app may automatically complete a file when Photoshop closes it.
        /// </summary>
        public static bool AUTO_COMPLETE_ON_DOC_CLOSE => false;

        // ==================== API SYNC ====================

        /// <summary>
        /// Interval between sync attempts (in seconds).
        /// </summary>
        public const int SYNC_INTERVAL_SECONDS = 2;

        /// <summary>
        /// Maximum retry attempts for failed API requests.
        /// </summary>
        public const int MAX_RETRY_ATTEMPTS = 3;

        /// <summary>
        /// API request timeout (in seconds).
        /// </summary>
        public const int API_TIMEOUT_SECONDS = 10;

        /// <summary>
        /// Runtime API timeout (in seconds), sourced from network appsettings when available.
        /// Falls back to API_TIMEOUT_SECONDS.
        /// </summary>
        public static int API_TIMEOUT_SECONDS_RUNTIME =>
            ReadIntFromNetworkSettings("ApiSettings", "TimeoutSeconds", API_TIMEOUT_SECONDS);

        /// <summary>
        /// Runtime auto-pause timeout (in seconds), sourced from network appsettings when available.
        /// Falls back to AUTO_PAUSE_NO_DOC_TIMEOUT.
        /// </summary>
        public static int AUTO_PAUSE_NO_DOC_TIMEOUT_RUNTIME =>
            ReadIntFromNetworkSettings("TrackingSettings", "AutoPauseNoDocTimeoutSeconds", AUTO_PAUSE_NO_DOC_TIMEOUT);

        /// <summary>
        /// Runtime max retry attempts, sourced from network appsettings when available.
        /// Falls back to MAX_RETRY_ATTEMPTS.
        /// </summary>
        public static int MAX_RETRY_ATTEMPTS_RUNTIME =>
            ReadIntFromNetworkSettings("ApiSettings", "MaxRetryAttempts", MAX_RETRY_ATTEMPTS);

        /// <summary>
        /// Runtime sync interval (seconds), sourced from network appsettings when available.
        /// Falls back to SYNC_INTERVAL_SECONDS.
        /// </summary>
        public static int SYNC_INTERVAL_SECONDS_RUNTIME =>
            ReadIntFromNetworkSettings("SyncSettings", "SyncIntervalSeconds", SYNC_INTERVAL_SECONDS);

        // ==================== FILE PATHS ====================

        public const string NETWORK_ROOT = @"P:\apps cache data";

        public static readonly string NETWORK_SETTINGS_DIR = Path.Combine(
            NETWORK_ROOT,
            "appsettings"
        );

        public static readonly string NETWORK_APPSETTINGS_FILE = Path.Combine(
            NETWORK_SETTINGS_DIR,
            "appsettings.json"
        );

        private static readonly object _networkSettingsLock = new();
        private static JsonDocument? _networkAppSettingsDoc;

        private static string? _currentAppUser;
        private static string? _currentAppRole;
        private static string? _currentDisplayName;

        private static string? _currentTrackerSessionId;
        private static string? _currentTrackerUserId;

        public static string CurrentAppUser
        {
            get
            {
                try
                {
                    var u = (_currentAppUser ?? string.Empty).Trim();
                    return string.IsNullOrWhiteSpace(u) ? "UnknownUser" : u;
                }
                catch
                {
                    return "UnknownUser";
                }
            }
        }

        public static string StorageUserSegment
        {
            get
            {
                try
                {
                    var display = EnsureDirectorySafe(_currentDisplayName);
                    if (!string.IsNullOrWhiteSpace(display)
                        && !string.Equals(display, "UnknownUser", StringComparison.OrdinalIgnoreCase))
                    {
                        return display;
                    }

                    var user = EnsureDirectorySafe(_currentAppUser);
                    if (!string.IsNullOrWhiteSpace(user)
                        && !string.Equals(user, "UnknownUser", StringComparison.OrdinalIgnoreCase))
                    {
                        return user;
                    }

                    return "_global";
                }
                catch
                {
                    return "_global";
                }
            }
        }

        public static string CurrentAppRole
        {
            get
            {
                try
                {
                    var r = (_currentAppRole ?? string.Empty).Trim();
                    return string.IsNullOrWhiteSpace(r) ? "Employee" : r;
                }
                catch
                {
                    return "Employee";
                }
            }
        }

        public static void SetCurrentAppRole(string? role)
        {
            try
            {
                var r = (role ?? string.Empty).Trim();
                _currentAppRole = string.IsNullOrWhiteSpace(r) ? "Employee" : r;
            }
            catch
            {
                _currentAppRole = "Employee";
            }
        }

        public static void SetCurrentAppUser(string? userName)
        {
            try
            {
                var u = EnsureDirectorySafe(userName);
                _currentAppUser = string.IsNullOrWhiteSpace(u) ? "UnknownUser" : u;
            }
            catch
            {
                _currentAppUser = "UnknownUser";
            }
        }

        public static string CurrentDisplayName
        {
            get
            {
                try
                {
                    var d = (_currentDisplayName ?? string.Empty).Trim();
                    return string.IsNullOrWhiteSpace(d) ? CurrentAppUser : d;
                }
                catch
                {
                    return CurrentAppUser;
                }
            }
        }

        public static void SetCurrentDisplayName(string? displayName)
        {
            try
            {
                _currentDisplayName = (displayName ?? string.Empty).Trim();
            }
            catch
            {
                _currentDisplayName = null;
            }
        }

        public static string? CurrentTrackerSessionId
        {
            get
            {
                try
                {
                    var v = (_currentTrackerSessionId ?? string.Empty).Trim();
                    return string.IsNullOrWhiteSpace(v) ? null : v;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static string? CurrentTrackerUserId
        {
            get
            {
                try
                {
                    var v = (_currentTrackerUserId ?? string.Empty).Trim();
                    return string.IsNullOrWhiteSpace(v) ? null : v;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void SetCurrentTrackerSession(string? sessionId, string? userId)
        {
            try
            {
                _currentTrackerSessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
            }
            catch
            {
                _currentTrackerSessionId = null;
            }

            try
            {
                _currentTrackerUserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
            }
            catch
            {
                _currentTrackerUserId = null;
            }
        }

        private static string EnsureDirectorySafe(string? value)
        {
            try
            {
                var v = (value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(v)) return string.Empty;

                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    v = v.Replace(c, '_');
                }

                return v;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Application data directory.
        /// </summary>
        public static string APP_DATA_DIR => Path.Combine(
            NETWORK_ROOT,
            EnsureDirectorySafe(StorageUserSegment)
        );

        /// <summary>
        /// Local application data directory.
        /// </summary>
        public static string LOCAL_APP_DATA_DIR => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCHL_Studio_V2",
            "apps cache data",
            EnsureDirectorySafe(StorageUserSegment)
        );

        public static string TODAY_DIR_NAME => DateTime.Now.ToString("yyyy-MM-dd");

        public static string DAILY_ROOT_DIR => Path.Combine(APP_DATA_DIR, TODAY_DIR_NAME);

        public static string GLOBAL_DIR => Path.Combine(NETWORK_ROOT, "_global");

        public static string GLOBAL_LOCKS_DIR => Path.Combine(GLOBAL_DIR, "locks");

        public static string GLOBAL_DATA_DIR => Path.Combine(GLOBAL_DIR, "data");

        public static string QUEUE_DIR => Path.Combine(DAILY_ROOT_DIR, "queue");

        /// <summary>
        /// Data directory for queue and cache files.
        /// </summary>
        public static string DATA_DIR => Path.Combine(DAILY_ROOT_DIR, "data");

        /// <summary>
        /// Sync queue file path.
        /// </summary>
        public static string SYNC_QUEUE_FILE => Path.Combine(QUEUE_DIR, "sync_queue.json");

        /// <summary>
        /// Order cache file path.
        /// </summary>
        public static string ORDER_CACHE_FILE => Path.Combine(DATA_DIR, "order_cache.json");

        /// <summary>
        /// Maximum items to keep in order cache (LRU).
        /// </summary>
        public const int MAX_ORDER_CACHE_ITEMS = 10;

        // ==================== WORK CATEGORIES ====================

        /// <summary>
        /// Available work categories for tracking.
        /// </summary>
        public static readonly List<string> DEFAULT_CATEGORIES = [
            "Ghost Mannequine",
            "Selection",
            "Color correction",
            "Retouch",
            "Shadow",
            "CP",
            "Multipath",
            "Resize",
            "Pattern change",
            "Color change",
            "Simple retouch",
            "High-end retouch",
            "Liquify",
            "Masking",
            "Dusting",
            "Cropping",
            "Background change",
            "Transparent background.",
            "Complex"
        ];

        // ==================== PAUSE REASONS ====================

        /// <summary>
        /// Available pause reasons for manual pauses.
        /// </summary>
        public static readonly List<string> DEFAULT_PAUSE_REASONS = [
            "Select Reason",
            "Breakfast",
            "Lunch",
            "Dinner",
            "Toilet",
            "Namaz",
            "Meeting",
            "Other..."
        ];

        // ==================== WORK TYPES ====================

        /// <summary>
        /// Available work types for Employee role (Production is default, not shown in menu).
        /// </summary>
        public static readonly List<string> WORK_TYPES_EMPLOYEE = ["Additional", "Test File"];

        /// <summary>
        /// Available work types for QC role.
        /// </summary>
        public static readonly List<string> WORK_TYPES_QC = ["QC 1", "QC 2", "QC AC", "Additional", "Test File"];

        // ==================== PHOTOSHOP MONITORING ====================



        /// <summary>
        /// Photoshop polling interval (in seconds).
        /// </summary>
        public const int PS_MONITOR_INTERVAL_SECONDS = 1;

        /// <summary>
        /// Number of consecutive missing checks before auto-completing a file.
        /// </summary>
        public const int FILE_MISSING_THRESHOLD = 3;

        // ==================== IDLE DETECTION ====================

        /// <summary>
        /// Idle timeout (in seconds) before auto-pausing.
        /// </summary>
        public const int IDLE_TIMEOUT_SECONDS = 10;

        // ==================== UI SETTINGS ====================

        /// <summary>
        /// Maximum filename length for display (characters).
        /// </summary>
        public const int MAX_FILENAME_DISPLAY_LENGTH = 40;

        /// <summary>
        /// Timer update interval (in milliseconds).
        /// </summary>
        public const int TIMER_UPDATE_INTERVAL_MS = 1000;

        // ==================== FOLDER NAMES ====================

        /// <summary>
        /// Done folder names by work type.
        /// </summary>
        public static readonly Dictionary<string, string> DONE_FOLDER_NAMES = new()
        {
            { "qc", "QC Done" },
            { "additional", "AD Production Done" },
            { "test file", "TF Production Done" },
            { "employee", "Done" }
        };

        /// <summary>
        /// Raw folder name for older file versions.
        /// </summary>
        public const string RAW_FOLDER_NAME = "Raw";

        // ==================== PHOTOSHOP VERSIONS ====================

        /// <summary>
        /// Supported Photoshop versions.
        /// </summary>
        public static readonly Dictionary<string, PhotoshopVersion> PHOTOSHOP_VERSIONS = new()
        {
            {
                "v25", new PhotoshopVersion
                {
                    Name = "Photoshop 2024 (v25)",
                    ProgId = "Photoshop.Application.190"
                }
            },
            {
                "v26", new PhotoshopVersion
                {
                    Name = "Photoshop 2025 (v26)",
                    ProgId = "Photoshop.Application.200"
                }
            }
        };

        /// <summary>
        /// Default Photoshop version.
        /// </summary>
        public const string DEFAULT_PS_VERSION = "v26";

        // ==================== SHIFT SCHEDULE ====================

        /// <summary>
        /// Work shift time ranges (24-hour format).
        /// </summary>
        public static readonly Dictionary<string, (int Start, int End)> SHIFT_SCHEDULE = new()
        {
            { "morning", (7, 15) },   // 7 AM - 3 PM
            { "evening", (15, 23) },  // 3 PM - 11 PM
            { "night", (23, 7) }      // 11 PM - 7 AM
        };

        // ==================== APPLICATION INFO ====================

        public const string APP_NAME = "SCHL Time Tracker";
        public const string APP_ID = "studio.clickhouse.schl.app.v1";
        public const string APP_ICON = "app_icon.ico";

        public static readonly string UPDATE_GITHUB_TOKEN = Environment.GetEnvironmentVariable("UPDATE_GITHUB_TOKEN") ?? string.Empty;

        // ==================== PROCESS IDs ====================
        public static readonly double UPDATE_CHECK_INTERVAL_HOURS = double.TryParse(Environment.GetEnvironmentVariable("UPDATE_CHECK_INTERVAL_HOURS"), out var hours)
            ? hours
            : 24;

        /// <summary>
        /// Get application version from environment or registry.
        /// </summary>
        public static string GetAppVersion()
        {
            // Check environment variable first
            var envVersion = Environment.GetEnvironmentVariable("APP_VERSION");
            if (!string.IsNullOrWhiteSpace(envVersion))
            {
                return envVersion;
            }

            // Check Windows Registry
            try
            {
#pragma warning disable CA1416
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\SCHL App");
                if (key != null)
                {
                    var regVersion = key.GetValue("AppVersion") as string;
                    if (!string.IsNullOrWhiteSpace(regVersion))
                    {
                        return regVersion;
                    }
                }
#pragma warning restore CA1416
            }
            catch
            {
                // Silent fail to fallback
            }

            // Check assembly version metadata
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                var infoVersion = asm
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (!string.IsNullOrWhiteSpace(infoVersion))
                {
                    var normalizedInfo = infoVersion.Split('+')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(normalizedInfo))
                    {
                        return normalizedInfo;
                    }
                }

                var fileVersion = asm
                    .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                    .Version;

                if (!string.IsNullOrWhiteSpace(fileVersion))
                {
                    return fileVersion.Trim();
                }

                var nameVersion = asm.GetName().Version?.ToString();
                if (!string.IsNullOrWhiteSpace(nameVersion))
                {
                    return nameVersion.Trim();
                }
            }
            catch
            {
                // Silent fail to fallback
            }

            return "1.0.2";
        }

        // ==================== API CONFIGURATION (SECURITY LAYER) ====================

        /// <summary>
        /// API base URL is sourced from the shared network appsettings.json.
        /// </summary>
        public static string? GetApiBaseUrl()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_apiBaseUrlCache))
                {
                    return _apiBaseUrlCache;
                }
            }
            catch
            {
            }

            // Network appsettings.json (admin-controlled)
            try
            {
                var doc = GetNetworkAppSettingsDocument();
                if (doc is not null)
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("ApiBaseUrl", out var apiEl))
                    {
                        var api = (apiEl.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(api))
                        {
                            _apiBaseUrlCache = api;
                            return api;
                        }
                    }

                    // Also support nested structure: { "Api": { "BaseUrl": "..." } }
                    if (root.TryGetProperty("Api", out var apiObj)
                        && apiObj.ValueKind == JsonValueKind.Object
                        && apiObj.TryGetProperty("BaseUrl", out var baseEl))
                    {
                        var api = (baseEl.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(api))
                        {
                            _apiBaseUrlCache = api;
                            return api;
                        }
                    }

                    // Also support: { "ApiSettings": { "BaseUrl": "..." } }
                    if (root.TryGetProperty("ApiSettings", out var apiSettingsObj)
                        && apiSettingsObj.ValueKind == JsonValueKind.Object
                        && apiSettingsObj.TryGetProperty("BaseUrl", out var settingsBaseEl))
                    {
                        var api = (settingsBaseEl.GetString() ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(api))
                        {
                            _apiBaseUrlCache = api;
                            return api;
                        }
                    }
                }
            }
            catch
            {
                // Silent fail
            }

            // Fallback to null (app will show error)
            return null;
        }

        /// <summary>
        /// Tracker secret used for the tracker security layer.
        /// Sourced from the shared network appsettings.json (TrackingSettings:TrackerSecret).
        /// </summary>
        public static string? GetTrackerSecret()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_trackerSecretCache))
                {
                    return _trackerSecretCache;
                }
            }
            catch
            {
            }

            try
            {
                var doc = GetNetworkAppSettingsDocument();
                if (doc is null)
                {
                    return null;
                }

                var root = doc.RootElement;

                if (root.TryGetProperty("TrackingSettings", out var trackingObj)
                    && trackingObj.ValueKind == JsonValueKind.Object
                    && trackingObj.TryGetProperty("TrackerSecret", out var secretEl))
                {
                    var secret = (secretEl.GetString() ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        _trackerSecretCache = secret;
                        return secret;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? _apiBaseUrlCache;

        private static string? _trackerSecretCache;

        private static string? TryGetDotEnvValue(string key)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var candidates = new List<string>
                {
                    Path.Combine(baseDir, ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), ".env")
                };

                // Also search parent directories so the app can be run from either
                // the solution root or the project folder.
                try
                {
                    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
                    for (var i = 0; i < 6 && current is not null; i++)
                    {
                        candidates.Add(Path.Combine(current.FullName, ".env"));
                        current = current.Parent;
                    }
                }
                catch
                {
                }

                foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    foreach (var line in File.ReadAllLines(path))
                    {
                        var trimmed = (line ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        {
                            continue;
                        }

                        var idx = trimmed.IndexOf('=');
                        if (idx <= 0)
                        {
                            continue;
                        }

                        var k = trimmed.Substring(0, idx).Trim();
                        if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var v = trimmed.Substring(idx + 1).Trim().Trim('"');
                        return string.IsNullOrWhiteSpace(v) ? null : v;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static int ReadIntFromNetworkSettings(string sectionName, string keyName, int fallback)
        {
            try
            {
                var doc = GetNetworkAppSettingsDocument();
                if (doc is null)
                {
                    return fallback;
                }

                if (!doc.RootElement.TryGetProperty(sectionName, out var section)
                    || section.ValueKind != JsonValueKind.Object)
                {
                    return fallback;
                }

                if (!section.TryGetProperty(keyName, out var value))
                {
                    return fallback;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
                {
                    return n;
                }

                if (value.ValueKind == JsonValueKind.String
                    && int.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static JsonDocument? GetNetworkAppSettingsDocument()
        {
            try
            {
                if (_networkAppSettingsDoc is not null)
                {
                    return _networkAppSettingsDoc;
                }
            }
            catch
            {
            }

            lock (_networkSettingsLock)
            {
                try
                {
                    if (_networkAppSettingsDoc is not null)
                    {
                        return _networkAppSettingsDoc;
                    }

                    if (!File.Exists(NETWORK_APPSETTINGS_FILE))
                    {
                        return null;
                    }

                    var json = File.ReadAllText(NETWORK_APPSETTINGS_FILE);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return null;
                    }

                    _networkAppSettingsDoc = JsonDocument.Parse(json);
                    return _networkAppSettingsDoc;
                }
                catch
                {
                    return null;
                }
            }
        }

        // ==================== HELPER FUNCTIONS ====================

        /// <summary>
        /// Get the appropriate 'Done' folder name based on work type.
        /// </summary>
        public static string GetDoneFolderName(string workType)
        {
            var key = (workType ?? "").ToLowerInvariant();
            return DONE_FOLDER_NAMES.TryGetValue(key, out var folderName) ? folderName : "Done";
        }

        /// <summary>
        /// Ensure all required data directories exist.
        /// </summary>
        public static void EnsureDataDirectories()
        {
            Directory.CreateDirectory(DATA_DIR);
        }
    }

    /// <summary>
    /// Photoshop version information.
    /// </summary>
    public class PhotoshopVersion
    {
        public string Name { get; set; } = string.Empty;
        public string ProgId { get; set; } = string.Empty;
    }
}
