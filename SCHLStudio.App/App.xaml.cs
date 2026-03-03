using System.Windows;
using System;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SCHLStudio.App.Services;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.Configuration;
using SCHLStudio.App.Shared.Services;
using SCHLStudio.App.Services.Diagnostics;

using SCHLStudio.App.Views.ExplorerV2.Services;
using SCHLStudio.App.Views.Login;
using SCHLStudio.App.Views.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace SCHLStudio.App
{
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;
        public IServiceProvider ServiceProvider => _serviceProvider!;
        private ServiceProvider? _serviceProvider;

        private const string SingleInstanceMutexName = "Local\\SCHL_Tracker_SingleInstance";
        private const string SingleInstancePipeName = "SCHL_Tracker_SingleInstance_Activate";
        private CancellationTokenSource? _singleInstancePipeCts;

        private static readonly object _startupLogLock = new();

        static App()
        {
            StartupLog("App static ctor");
        }

        public App()
        {
            try
            {
                StartupLog("App ctor begin");

                AppStartupLifecycle.AttachFirstChanceLogging(StartupLog);

                InitializeComponent();
                StartupLog("App ctor after InitializeComponent");
            }
            catch (Exception ex)
            {
                StartupLog($"App ctor exception: {ex}");
                try
                {
                    MessageBox.Show(ex.ToString(), "App Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "App.Ctor.ShowStartupError",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        private static void StartupLog(string message)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "SCHLStudio.App");
                Directory.CreateDirectory(tempDir);

                var path = Path.Combine(tempDir, "startup.log");
                lock (_startupLogLock)
                {
                    File.AppendAllText(path, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "StartupLog",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["message"] = message
                        });
                }
                catch
                {
                }
            }
        }

        private bool _suppressExitOnClose;

        protected override void OnStartup(StartupEventArgs e)
        {
            StartupLog("OnStartup begin");


            IConfiguration configuration;
            try
            {
                configuration = BuildConfiguration();
            }
            catch (Exception ex)
            {
                try
                {
                    StartupLog($"BuildConfiguration failed. Network config file may be missing/unreachable: {AppConfig.NETWORK_APPSETTINGS_FILE}. Error: {ex}");
                }
                catch
                {
                    NonCriticalLog.IncrementAndLog("App", "OnStartup.BuildConfiguration.StartupLog");
                }

                try
                {
                    MessageBox.Show(
                        "Cannot connect to the database configuration. Please ensure you are connected to the company network or contact the administrator.",
                        "Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    NonCriticalLog.IncrementAndLog("App", "OnStartup.ShowConfigurationError");
                }

                Shutdown(1);
                return;
            }
            AppStartupLifecycle.AttachUnhandledExceptionHandlers(this, configuration, StartupLog);

            base.OnStartup(e);

            if (!TryAcquireSingleInstanceLock())
            {
                StartupLog("Single instance lock not acquired");
                TrySignalExistingInstance();
                Shutdown(0);
                return;
            }

            StartupLog("Single instance lock acquired");

            StartSingleInstancePipeServer();


            TrySetDpiAwareness();
            TrySetAppUserModelId(AppConfig.APP_ID);

            // Verify API URL is configured (matches Python config.py security layer)
            var apiBaseUrl = AppConfig.GetApiBaseUrl();
            StartupLog($"GetApiBaseUrl: {(string.IsNullOrWhiteSpace(apiBaseUrl) ? "<empty>" : apiBaseUrl)}");
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                MessageBox.Show(
                    "API URL is not configured.\n\nPlease contact admin to verify the connection settings.",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            if (!IsSecureApiBaseUrl(apiBaseUrl, out var apiUrlValidationMessage))
            {
                MessageBox.Show(
                    apiUrlValidationMessage,
                    "Security Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // Clean up old local data in background (don't block UI startup)
            _ = System.Threading.Tasks.Task.Run(() => TryCleanupLocalData());


            _serviceProvider = BuildServices(configuration);
            StartupLog("ServiceProvider built");

            try
            {
                _serviceProvider.GetRequiredService<IThemeService>().Initialize();
                StartupLog("ThemeService initialized");
            }
            catch
            {
                NonCriticalLog.IncrementAndLog("App", "OnStartup.ThemeService.Initialize");
                StartupLog("ThemeService initialize failed");
            }


            ShowLoginWindow();
            StartupLog("LoginWindow shown");
        }



        internal void ShowLoginWindow()
        {
            try
            {
                var loginWindow = _serviceProvider?.GetRequiredService<LoginWindow>();
                if (loginWindow is null)
                {
                    StartupLog("LoginWindow resolved null");
                    return;
                }

                MainWindow = loginWindow;
                loginWindow.Show();

                StartupLog("LoginWindow shown");

                AttachTrayBehavior(loginWindow);
            }
            catch (Exception ex)
            {
                StartupLog($"ShowLoginWindow exception: {ex}");
                try
                {
                    MessageBox.Show(ex.ToString(), "LoginWindow Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "ShowLoginWindow.ShowError",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }
            }
        }

        internal void BeginLogout()
        {
            _suppressExitOnClose = true;
        }

        internal void SetCurrentMainWindow(Window window)
        {
            try
            {
                MainWindow = window;
                AttachTrayBehavior(window);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "SetCurrentMainWindow",
                        ex: ex);
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "SetCurrentMainWindow.LogFailure",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void AttachTrayBehavior(Window window)
        {
            try
            {
                window.StateChanged -= Window_StateChanged;
                window.Closing -= Window_Closing;

                window.StateChanged += Window_StateChanged;
                window.Closing += Window_Closing;
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "AttachTrayBehavior",
                        ex: ex);
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "AttachTrayBehavior.LogFailure",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            try
            {
                if (sender is not Window)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "Window_StateChanged",
                        ex: ex);
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "Window_StateChanged.LogFailure",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_suppressExitOnClose)
                {
                    _suppressExitOnClose = false;
                    return;
                }

                var isLoggedIn = false;
                try
                {
                    // If no active session/user, closing should exit (e.g., user closed Login window).
                    isLoggedIn = !string.IsNullOrWhiteSpace(AppConfig.CurrentTrackerSessionId)
                        && !string.Equals(AppConfig.CurrentAppUser, "UnknownUser", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    isLoggedIn = false;
                }

                if (!isLoggedIn)
                {
                    return;
                }

                // Close (X) should NOT logout or exit.
                // Instead, keep the app running in background and just hide the window.
                if (sender is Window w)
                {
                    e.Cancel = true;
                    try
                    {
                        w.WindowState = WindowState.Minimized;
                        w.Hide();
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "Window_Closing",
                        ex: ex);
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "Window_Closing.LogFailure",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                try
                {
                    var apiClient = ServiceProvider?.GetService(typeof(IApiClient)) as IApiClient;
                    if (apiClient is not null)
                    {
                        var drained = false;
                        Task<bool>? drainTask = null;

                        try
                        {
                            drainTask = apiClient.WaitForSyncAsync(5);
                            if (drainTask.Wait(TimeSpan.FromSeconds(6)))
                            {
                                drained = drainTask.Result;
                            }
                            else
                            {
                                try
                                {
                                    AppDataLog.LogEvent(
                                        area: "App",
                                        operation: "OnExit.ApiClient.WaitForSync.Timeout",
                                        level: "warn",
                                        data: new System.Collections.Generic.Dictionary<string, string?>
                                        {
                                            ["message"] = "Timed out waiting for tracker queue drain task completion"
                                        });
                                }
                                catch
                                {
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                AppDataLog.LogError(
                                    area: "App",
                                    operation: "OnExit.ApiClient.WaitForSync",
                                    ex: ex);
                            }
                            catch
                            {
                            }
                        }

                        if (!drained)
                        {
                            try
                            {
                                AppDataLog.LogEvent(
                                    area: "App",
                                    operation: "OnExit.ApiClient.WaitForSync",
                                    level: "warn",
                                    data: new System.Collections.Generic.Dictionary<string, string?>
                                    {
                                        ["message"] = "Tracker queue did not fully drain before timeout"
                                    });
                            }
                            catch
                            {
                            }
                        }

                        apiClient.Stop();
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "OnExit.ApiClient.Stop",
                            ex: ex);
                    }
                    catch
                    {
                    }
                }

            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "OnExit.HandlerFailure",
                        ex: ex);
                }
                catch
                {
                }
            }

            try
            {
                _singleInstancePipeCts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _singleInstancePipeCts?.Dispose();
                _singleInstancePipeCts = null;
            }
            catch
            {
            }

            base.OnExit(e);

            try
            {
                if (_ownsSingleInstanceMutex && _singleInstanceMutex is not null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
            }
            catch
            {
            }

            try
            {
                _singleInstanceMutex?.Dispose();
            }
            catch
            {
            }
        }

        private void StartSingleInstancePipeServer()
        {
            try
            {
                _singleInstancePipeCts?.Cancel();
            }
            catch
            {
            }

            try
            {
                _singleInstancePipeCts?.Dispose();
            }
            catch
            {
            }

            _singleInstancePipeCts = new CancellationTokenSource();
            var token = _singleInstancePipeCts.Token;

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(
                            pipeName: SingleInstancePipeName,
                            direction: PipeDirection.In,
                            maxNumberOfServerInstances: 1,
                            transmissionMode: PipeTransmissionMode.Message,
                            options: PipeOptions.Asynchronous);

                        await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                        using var reader = new StreamReader(server, Encoding.UTF8);
                        var msg = await reader.ReadToEndAsync(token).ConfigureAwait(false);

                        if (string.Equals((msg ?? string.Empty).Trim(), "SHOW", StringComparison.OrdinalIgnoreCase))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    var w = MainWindow;
                                    if (w == null) return;

                                    w.Show();
                                    if (w.WindowState == WindowState.Minimized)
                                    {
                                        w.WindowState = WindowState.Normal;
                                    }

                                    w.Activate();
                                    w.Topmost = true;
                                    w.Topmost = false;
                                    w.Focus();
                                }
                                catch
                                {
                                }
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                    }
                }
            }, token);
        }

        private void TrySignalExistingInstance()
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: SingleInstancePipeName,
                    direction: PipeDirection.Out);

                client.Connect(timeout: 500);

                using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                writer.Write("SHOW");
            }
            catch
            {
            }
        }

        private static void TryCleanupLocalData()
        {
            try
            {
                // Use AppConfig constants instead of configuration
                Directory.CreateDirectory(AppConfig.GLOBAL_DATA_DIR);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "TryCleanupLocalData",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["dataDir"] = AppConfig.GLOBAL_DATA_DIR
                        });
                }
                catch (Exception logEx)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "TryCleanupLocalData.LogFailure",
                            ex: logEx);
                    }
                    catch
                    {
                    }
                }
            }

            try
            {
                CleanupOldDailyFolders(AppConfig.APP_DATA_DIR);
            }
            catch { }

            try
            {
                CleanupOldDailyFolders(AppConfig.LOCAL_APP_DATA_DIR);
            }
            catch { }
        }

        private static void CleanupOldDailyFolders(string rootDir)
        {
            if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
                return;

            var cutoffDate = DateTime.Now.Date.AddDays(-2);

            var directories = Directory.GetDirectories(rootDir);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (DateTime.TryParseExact(dirName, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dirDate))
                {
                    if (dirDate.Date < cutoffDate)
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                AppDataLog.LogError(
                                    area: "App",
                                    operation: "CleanupOldDailyFolders",
                                    ex: ex,
                                    data: new System.Collections.Generic.Dictionary<string, string?>
                                    {
                                        ["dir"] = dir
                                    });
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private static ServiceProvider BuildServices(IConfiguration configuration)
        {
            var services = new ServiceCollection();

            services.AddSingleton(configuration);
            services.AddSingleton<HttpClient>();

            services.AddSingleton<IApiClient>(sp =>
            {
                // Use AppConfig for all settings (matches Python config.py)
                var baseUrl = AppConfig.GetApiBaseUrl() ?? string.Empty;

                var queueFile = AppConfig.SYNC_QUEUE_FILE;
                var historyFile = Path.Combine(AppConfig.DATA_DIR, "worklog_history.jsonl");

                var httpClient = sp.GetRequiredService<HttpClient>();
                httpClient.Timeout = TimeSpan.FromSeconds(AppConfig.API_TIMEOUT_SECONDS_RUNTIME);

                try
                {
                    var secret = (configuration?["TrackingSettings:TrackerSecret"] ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        try
                        {
                            httpClient.DefaultRequestHeaders.Remove("tracker-secret");
                        }
                        catch
                        {
                        }
                        httpClient.DefaultRequestHeaders.Add("tracker-secret", secret);
                    }
                }
                catch
                {
                }

                _ = queueFile;
                _ = historyFile;

                return new ApiClient(httpClient, baseUrl);
            });
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IExplorerV2WorkflowService, ExplorerV2WorkflowService>();

            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<Func<MainWindow>>(sp => () => sp.GetRequiredService<MainWindow>());

            return services.BuildServiceProvider();
        }

        private bool TryAcquireSingleInstanceLock()
        {
            try
            {
                _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var createdNew);
                _ownsSingleInstanceMutex = createdNew;

                if (!createdNew)
                {
                    try
                    {
                        _singleInstanceMutex.Dispose();
                    }
                    catch
                    {
                    }
                    _singleInstanceMutex = null;
                }

                return createdNew;
            }
            catch
            {
                NonCriticalLog.IncrementAndLog("App", "TryAcquireSingleInstanceLock");
                _ownsSingleInstanceMutex = false;
                return false;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void TrySetDpiAwareness()
        {
            try
            {
                _ = SetProcessDpiAwareness(1);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "TrySetDpiAwareness.SetProcessDpiAwareness",
                        ex: ex);
                }
                catch
                {
                }
                try
                {
                    _ = SetProcessDPIAware();
                }
                catch (Exception ex2)
                {
                    try
                    {
                        AppDataLog.LogError(
                            area: "App",
                            operation: "TrySetDpiAwareness.SetProcessDPIAware",
                            ex: ex2);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static IConfiguration BuildConfiguration()
        {
            // Network-based configuration (admin-controlled)
            if (!File.Exists(AppConfig.NETWORK_APPSETTINGS_FILE))
            {
                throw new FileNotFoundException(
                    "Missing network configuration file",
                    AppConfig.NETWORK_APPSETTINGS_FILE);
            }

            return new ConfigurationBuilder()
                .SetBasePath(AppConfig.NETWORK_SETTINGS_DIR)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
        }

        private static bool IsSecureApiBaseUrl(string apiBaseUrl, out string validationMessage)
        {
            validationMessage = string.Empty;

            try
            {
                if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri))
                {
                    validationMessage = "Configured API URL is invalid. Please provide a valid absolute URL.";
                    return false;
                }

                if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                validationMessage = "Configured API URL must use http or https.";
                return false;
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "IsSecureApiBaseUrl",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["apiBaseUrl"] = apiBaseUrl
                        });
                }
                catch
                {
                }

                validationMessage = "Unable to validate API URL security settings.";
                return false;
            }
        }

        // Removed: IsApiConfigured - now using AppConfig.GetApiBaseUrl() directly

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void TrySetAppUserModelId(string appId)
        {
            try
            {
                _ = SetCurrentProcessExplicitAppUserModelID(appId);
            }
            catch (Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "TrySetAppUserModelId",
                        ex: ex,
                        data: new System.Collections.Generic.Dictionary<string, string?>
                        {
                            ["appId"] = appId
                        });
                }
                catch
                {
                }
            }
        }

        #pragma warning disable SYSLIB1054
        [DllImport("shell32.dll", SetLastError = true)]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

        [DllImport("user32.dll")]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static extern int SetProcessDpiAwareness(int value);
        #pragma warning restore SYSLIB1054
    }
}