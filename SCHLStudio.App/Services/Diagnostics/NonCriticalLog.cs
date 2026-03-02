using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SCHLStudio.App.Services.Diagnostics
{
    internal static class NonCriticalLog
    {
        private static readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Channel<ErrorLogRequest> _errorQueue = Channel.CreateBounded<ErrorLogRequest>(
            new BoundedChannelOptions(2048)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        private static int _workerStarted;
        private static int _droppedLogCount;

        private readonly record struct ErrorLogRequest(
            string Area,
            string Operation,
            Exception Exception,
            IReadOnlyDictionary<string, string?>? Data,
            string? UserName);

        private static void EnsureWorkerStarted()
        {
            if (Interlocked.Exchange(ref _workerStarted, 1) == 1)
            {
                return;
            }

            _ = Task.Factory.StartNew(
                async () => await ProcessQueueAsync().ConfigureAwait(false),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private static async Task ProcessQueueAsync()
        {
            try
            {
                while (await _errorQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_errorQueue.Reader.TryRead(out var req))
                    {
                        try
                        {
                            AppDataLog.LogError(req.Area, req.Operation, req.Exception, req.Data, req.UserName);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        internal static void EnqueueError(
            string area,
            string operation,
            Exception ex,
            IReadOnlyDictionary<string, string?>? data = null,
            string? userName = null)
        {
            try
            {
                if (ex is null)
                {
                    return;
                }

                var a = (area ?? string.Empty).Trim();
                var op = (operation ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(a)) a = "General";
                if (string.IsNullOrWhiteSpace(op)) op = "Unknown";

                EnsureWorkerStarted();
                if (!_errorQueue.Writer.TryWrite(new ErrorLogRequest(a, op, ex, data, userName)))
                {
                    Interlocked.Increment(ref _droppedLogCount);
                }
            }
            catch
            {
            }
        }

        internal static int IncrementAndLog(
            string area,
            string operation,
            Exception? ex = null,
            IReadOnlyDictionary<string, string?>? data = null,
            string? userName = null)
        {
            try
            {
                var a = (area ?? string.Empty).Trim();
                var op = (operation ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(a)) a = "General";
                if (string.IsNullOrWhiteSpace(op)) op = "Unknown";

                var key = a + "::" + op;
                var count = _counters.AddOrUpdate(key, 1, static (_, current) => current + 1);

                var payload = new Dictionary<string, string?>
                {
                    ["count"] = count.ToString()
                };

                if (data is not null)
                {
                    foreach (var kv in data)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key))
                        {
                            payload[kv.Key] = kv.Value;
                        }
                    }
                }

                if (ex is not null)
                {
                    payload["exceptionType"] = ex.GetType().Name;
                    payload["exceptionMessage"] = ex.Message;
                }

                AppDataLog.LogEvent(a, op, "warn", payload, userName);
                return count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
