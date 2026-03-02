using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace SCHLStudio.App.Services.Diagnostics
{
    internal static class AppStartupLifecycle
    {
        internal static void AttachFirstChanceLogging(Action<string> startupLog)
        {
            try
            {
                if (!ShouldEnableFirstChanceLogging())
                {
                    return;
                }

                AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
                {
                    try
                    {
                        startupLog($"FirstChanceException: {args.Exception.GetType().Name}: {args.Exception.Message}");
                    }
                    catch (Exception ex)
                    {
                        TryLogError("App", "AppDomain.FirstChanceException.StartupLog", ex);
                    }
                };
            }
            catch (Exception ex)
            {
                TryLogError("App", "AppStartupLifecycle.AttachFirstChanceLogging", ex);
            }
        }

        internal static void AttachUnhandledExceptionHandlers(System.Windows.Application app, IConfiguration configuration, Action<string> startupLog)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                {
                    try
                    {
                        Console.WriteLine($"Unhandled Exception: {args.ExceptionObject}");
                        startupLog($"UnhandledException: {args.ExceptionObject}");

                        var ex = args.ExceptionObject as Exception
                                 ?? new Exception(args.ExceptionObject?.ToString() ?? "Unhandled exception");

                        TryLogError(
                            "App",
                            "AppDomain.UnhandledException",
                            ex,
                            new Dictionary<string, string?>
                            {
                                ["IsTerminating"] = args.IsTerminating.ToString()
                            });
                    }
                    catch (Exception ex)
                    {
                        TryLogError("App", "AppDomain.UnhandledException.HandlerFailure", ex);
                    }
                };

                app.DispatcherUnhandledException += (_, args) =>
                {
                    try
                    {
                        Console.WriteLine($"Dispatcher Exception: {args.Exception}");
                        startupLog($"DispatcherUnhandledException: {args.Exception}");

                        TryLogError(
                            "App",
                            "Application.DispatcherUnhandledException",
                            args.Exception,
                            new Dictionary<string, string?>
                            {
                                ["Handled"] = args.Handled.ToString()
                            });
                    }
                    catch (Exception ex)
                    {
                        TryLogError("App", "DispatcherUnhandledException.HandlerFailure", ex);
                    }

                    var handled = ShouldHandleDispatcherExceptionByDefault(args.Exception);
                    try
                    {
                        var raw = configuration["ExceptionHandling:DispatcherUnhandledException:Handled"];
                        if (!string.IsNullOrWhiteSpace(raw) && bool.TryParse(raw, out var parsed))
                        {
                            handled = parsed;
                        }
                    }
                    catch
                    {
                        NonCriticalLog.IncrementAndLog("App", "DispatcherUnhandledException.Policy.ReadConfig");
                        handled = ShouldHandleDispatcherExceptionByDefault(args.Exception);
                    }

                    TryLogError(
                        "App",
                        "Application.DispatcherUnhandledException.Policy",
                        new Exception("DispatcherUnhandledException policy applied"),
                        new Dictionary<string, string?>
                        {
                            ["Handled"] = handled.ToString()
                        });

                    args.Handled = handled;
                };
            }
            catch (Exception ex)
            {
                TryLogError("App", "AppStartupLifecycle.AttachUnhandledExceptionHandlers", ex);
            }
        }

        private static bool ShouldEnableFirstChanceLogging()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("SCHL_LOG_FIRST_CHANCE");
                if (!string.IsNullOrWhiteSpace(env)
                    && (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

#if DEBUG
                return true;
#else
                return false;
#endif
            }
            catch
            {
                NonCriticalLog.IncrementAndLog("App", "ShouldEnableFirstChanceLogging");
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        private static bool ShouldHandleDispatcherExceptionByDefault(Exception? ex)
        {
            try
            {
                if (ex is null)
                {
                    return true;
                }

                if (ex is OutOfMemoryException
                    || ex is AccessViolationException
                    || ex is AppDomainUnloadedException
                    || ex is BadImageFormatException
                    || ex is CannotUnloadAppDomainException)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                NonCriticalLog.IncrementAndLog("App", "ShouldHandleDispatcherExceptionByDefault");
                return true;
            }
        }

        private static void TryLogError(string area, string operation, Exception ex, IReadOnlyDictionary<string, string?>? data = null)
        {
            try
            {
                AppDataLog.LogError(area, operation, ex, data);
            }
            catch
            {
            }
        }
    }
}
