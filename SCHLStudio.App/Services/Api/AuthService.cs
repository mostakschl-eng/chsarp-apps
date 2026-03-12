using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SCHLStudio.App.Services.Api
{
    public sealed class AuthService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public AuthService(HttpClient httpClient, string apiBaseUrl)
        {
            _httpClient = httpClient;

            var baseUrl = (apiBaseUrl ?? string.Empty).TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.EndsWith("/tracker", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl += "/tracker";
            }

            _apiBaseUrl = baseUrl;
        }

        public async Task<string> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var result = await LoginTypedAsync(username, password, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, JsonOptions);
        }

        public async Task<ApiLoginResult> LoginTypedAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new { username, password };
                var (status, body) = await PostJsonAsync("/login", payload, cancellationToken).ConfigureAwait(false);

                if (status == HttpStatusCode.OK || status == HttpStatusCode.Created)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("passwordSetupRequired", out var psr) && psr.ValueKind == JsonValueKind.True)
                        {
                            var uname = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                            return new ApiLoginResult
                            {
                                PasswordSetupRequired = true,
                                Username = uname
                            };
                        }

                        if (doc.RootElement.TryGetProperty("valid", out var valid) && valid.ValueKind == JsonValueKind.True)
                        {
                            var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() ?? "Employee" : "Employee";
                            var uname = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                            var dname = doc.RootElement.TryGetProperty("displayName", out var d) ? d.GetString() : null;
                            var sessionId = doc.RootElement.TryGetProperty("sessionId", out var s) ? s.GetString() : null;
                            string? userId = null;
                            if (doc.RootElement.TryGetProperty("user_id", out var uid))
                            {
                                userId = uid.ValueKind == JsonValueKind.String ? uid.GetString() : uid.ToString();
                            }

                            ActiveWorkData? activeWork = null;
                            try
                            {
                                if (doc.RootElement.TryGetProperty("activeWork", out var aw) && aw.ValueKind == JsonValueKind.Object)
                                {
                                    activeWork = new ActiveWorkData
                                    {
                                        ClientCode = aw.TryGetProperty("client_code", out var cc) ? cc.GetString() ?? string.Empty : string.Empty,
                                        FolderPath = aw.TryGetProperty("folder_path", out var fp) ? fp.GetString() ?? string.Empty : string.Empty,
                                        Shift = aw.TryGetProperty("shift", out var sh) ? sh.GetString() ?? string.Empty : string.Empty,
                                        WorkType = aw.TryGetProperty("work_type", out var wt) ? wt.GetString() ?? string.Empty : string.Empty,
                                        EstimateTime = aw.TryGetProperty("estimate_time", out var et) && et.ValueKind == JsonValueKind.Number ? et.GetInt32() : 0,
                                        Categories = aw.TryGetProperty("categories", out var ct) ? ct.GetString() ?? string.Empty : string.Empty,
                                        DoneTimeTotal = aw.TryGetProperty("done_time_total", out var dt) && dt.ValueKind == JsonValueKind.Number ? dt.GetInt32() : 0,
                                    };

                                    if (aw.TryGetProperty("files", out var filesArr) && filesArr.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var fEl in filesArr.EnumerateArray())
                                        {
                                            var awf = new ActiveWorkFile
                                            {
                                                FileName = fEl.TryGetProperty("file_name", out var fn) ? fn.GetString() ?? string.Empty : string.Empty,
                                                FilePath = fEl.TryGetProperty("file_path", out var fpath) ? fpath.GetString() ?? string.Empty : string.Empty,
                                                TimeSpent = fEl.TryGetProperty("time_spent", out var ts) && ts.ValueKind == JsonValueKind.Number ? ts.GetInt32() : 0,
                                            };

                                            if (fEl.TryGetProperty("started_at", out var sa) && sa.ValueKind == JsonValueKind.String)
                                            {
                                                if (DateTimeOffset.TryParse(sa.GetString(), out var parsed))
                                                {
                                                    awf.StartedAt = parsed;
                                                }
                                            }

                                            activeWork.Files.Add(awf);
                                        }
                                    }

                                    // If no working files, discard
                                    if (activeWork.Files.Count == 0) activeWork = null;
                                }
                            }
                            catch (Exception exAw)
                            {
                                LogNonCritical("LoginAsync.ParseActiveWork", exAw);
                                activeWork = null;
                            }

                            return new ApiLoginResult
                            {
                                Success = true,
                                Valid = true,
                                Role = role,
                                Username = uname,
                                DisplayName = dname,
                                SessionId = sessionId,
                                UserId = userId,
                                ActiveWork = activeWork,
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        LogNonCritical("LoginAsync.ParseSuccessResponse", ex);
                    }
                }

                return new ApiLoginResult
                {
                    Success = false,
                    Message = "Invalid credentials"
                };
            }
            catch (Exception ex)
            {
                return new ApiLoginResult
                {
                    Success = false,
                    Message = $"Connection error: {ex.Message}"
                };
            }
        }

        public async Task<bool> LogoutAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var s = (sessionId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return false;
                }

                var payload = new { sessionId = s };
                var (status, _) = await PostJsonAsync("/logout", payload, cancellationToken).ConfigureAwait(false);
                return status == HttpStatusCode.OK || status == HttpStatusCode.Created;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(LogoutAsync), ex);
                return false;
            }
        }

        public async Task<string> GetDashboardTodayAsync(string? username = null, string? date = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var u = (username ?? string.Empty).Trim();
                var payload = new System.Collections.Generic.Dictionary<string, string?>();

                if (!string.IsNullOrWhiteSpace(u))
                {
                    payload["username"] = u;
                }

                if (!string.IsNullOrWhiteSpace(date))
                {
                    payload["date"] = date;
                }

                var result = await PostJsonAsync("/dashboard-today", payload, cancellationToken).ConfigureAwait(false);
                if (result.Status == HttpStatusCode.OK || result.Status == HttpStatusCode.Created)
                {
                    return result.Body ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(GetDashboardTodayAsync), ex);
                return string.Empty;
            }
        }

        public async Task<string> SearchFileAsync(string query, string? clientCode = null, CancellationToken cancellationToken = default)
        {
            var result = await SearchFileTypedAsync(query, clientCode, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, JsonOptions);
        }

        public async Task<string> ReportFileAsync(ApiReportFileRequest request, CancellationToken cancellationToken = default)
        {
            var ok = await ReportFileTypedAsync(request, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = ok }, JsonOptions);
        }

        public async Task<bool> ReportFileTypedAsync(ApiReportFileRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (request is null)
                {
                    return false;
                }

                var payload = new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["employeeName"] = (request.EmployeeName ?? string.Empty).Trim(),
                    ["workType"] = (request.WorkType ?? string.Empty).Trim(),
                    ["shift"] = (request.Shift ?? string.Empty).Trim(),
                    ["clientCode"] = (request.ClientCode ?? string.Empty).Trim(),
                    ["folderPath"] = (request.FolderPath ?? string.Empty).Trim(),
                    ["dateToday"] = (request.DateToday ?? string.Empty).Trim(),
                    ["fileName"] = (request.FileName ?? string.Empty).Trim(),
                    ["report"] = (request.Report ?? string.Empty).Trim(),
                };

                if (string.IsNullOrWhiteSpace(payload["employeeName"]) || string.IsNullOrWhiteSpace(payload["fileName"]) || string.IsNullOrWhiteSpace(payload["dateToday"]))
                {
                    return false;
                }

                var result = await PostJsonAsync("/report-file", payload, cancellationToken).ConfigureAwait(false);
                if (result.Status == HttpStatusCode.OK || result.Status == HttpStatusCode.Created)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(result.Body ?? string.Empty);
                        if (doc.RootElement.TryGetProperty("success", out var s))
                        {
                            if (s.ValueKind == JsonValueKind.True) return true;
                            if (s.ValueKind == JsonValueKind.False) return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogNonCritical("ReportFileTypedAsync.Parse", ex);
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(ReportFileTypedAsync), ex);
                return false;
            }
        }

        public async Task<ApiSearchFileResult> SearchFileTypedAsync(string query, string? clientCode = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var q = (query ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(q))
                {
                    return new ApiSearchFileResult { Success = false };
                }

                var payload = new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["query"] = q
                };

                if (!string.IsNullOrWhiteSpace(clientCode))
                {
                    payload["clientCode"] = clientCode;
                }

                var result = await PostJsonAsync("/search-file", payload, cancellationToken).ConfigureAwait(false);
                if (result.Status == HttpStatusCode.OK || result.Status == HttpStatusCode.Created)
                {
                    if (string.IsNullOrWhiteSpace(result.Body))
                    {
                        try
                        {
                            Debug.WriteLine("[AuthService] SearchFileTypedAsync OK but empty body");
                        }
                        catch
                        {
                        }
                        return new ApiSearchFileResult { Success = false };
                    }

                    try
                    {
                        var parsed = JsonSerializer.Deserialize<ApiSearchFileResult>(result.Body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        var safe = parsed ?? new ApiSearchFileResult { Success = false };
                        try
                        {
                            if ((safe.Results?.Count ?? 0) == 0)
                            {
                                Debug.WriteLine("[AuthService] SearchFileTypedAsync OK but 0 results");
                            }
                        }
                        catch
                        {
                        }

                        return safe;
                    }
                    catch (Exception ex)
                    {
                        LogNonCritical("SearchFileTypedAsync.Deserialize", ex);
                        return new ApiSearchFileResult { Success = false };
                    }
                }

                try
                {
                    var bodyPrefix = (result.Body ?? string.Empty);
                    if (bodyPrefix.Length > 300) bodyPrefix = bodyPrefix[..300];
                    Debug.WriteLine($"[AuthService] SearchFileTypedAsync HTTP {(int)result.Status}: {bodyPrefix}");
                }
                catch
                {
                }

                return new ApiSearchFileResult { Success = false };
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(SearchFileAsync), ex);
                return new ApiSearchFileResult { Success = false };
            }
        }

        public async Task<string> SetPasswordAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var result = await SetPasswordTypedAsync(username, password, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, JsonOptions);
        }

        public async Task<ApiSetPasswordResult> SetPasswordTypedAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new { username, password };
                var (status, body) = await PostJsonAsync("/set-password", payload, cancellationToken).ConfigureAwait(false);

                if (status == HttpStatusCode.OK || status == HttpStatusCode.Created)
                {
                    string message = "Success";
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        {
                            message = m.GetString() ?? "Success";
                        }
                    }
                    catch (Exception ex)
                    {
                        LogNonCritical("SetPasswordAsync.ParseSuccessMessage", ex);
                    }

                    return new ApiSetPasswordResult
                    {
                        Success = true,
                        Message = message
                    };
                }

                string errorMsg = "Failed to set password";
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    {
                        errorMsg = m.GetString() ?? "Failed to set password";
                    }
                }
                catch (Exception ex)
                {
                    LogNonCritical("SetPasswordAsync.ParseErrorMessage", ex);
                }

                return new ApiSetPasswordResult
                {
                    Success = false,
                    Message = errorMsg
                };
            }
            catch (Exception ex)
            {
                return new ApiSetPasswordResult
                {
                    Success = false,
                    Message = $"Connection error: {ex.Message}"
                };
            }
        }

        public async Task<string> CheckUserAsync(string username, CancellationToken cancellationToken = default)
        {
            var result = await CheckUserTypedAsync(username, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Serialize(result, JsonOptions);
        }

        public async Task<ApiCheckUserResult> CheckUserTypedAsync(string username, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new { username };
                var (status, body) = await PostJsonAsync("/check-user", payload, cancellationToken).ConfigureAwait(false);

                if (status == HttpStatusCode.OK || status == HttpStatusCode.Created)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var passwordRequired = true;
                        if (doc.RootElement.TryGetProperty("passwordRequired", out var pr) && pr.ValueKind == JsonValueKind.True)
                        {
                            passwordRequired = true;
                        }
                        else if (doc.RootElement.TryGetProperty("passwordRequired", out var pr2) && pr2.ValueKind == JsonValueKind.False)
                        {
                            passwordRequired = false;
                        }

                        var uname = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                        var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() ?? "Employee" : "Employee";

                        return new ApiCheckUserResult
                        {
                            Exists = true,
                            PasswordRequired = passwordRequired,
                            Username = uname,
                            Role = role
                        };
                    }
                    catch (Exception ex)
                    {
                        LogNonCritical("CheckUserAsync.ParseSuccessResponse", ex);
                    }
                }

                return new ApiCheckUserResult
                {
                    Exists = false
                };
            }
            catch (Exception ex)
            {
                return new ApiCheckUserResult
                {
                    Exists = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<string> GetJobListAsync(string? clientCode = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new System.Collections.Generic.Dictionary<string, string?>();
                if (!string.IsNullOrWhiteSpace(clientCode))
                {
                    payload["clientCode"] = clientCode;
                }

                var result = await PostJsonAsync("/job-list", payload, cancellationToken).ConfigureAwait(false);
                if (result.Status == HttpStatusCode.OK || result.Status == HttpStatusCode.Created)
                {
                    return result.Body ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(GetJobListAsync), ex);
                return string.Empty;
            }
        }

        private static void LogNonCritical(string operation, Exception ex)
        {
            try
            {
                Debug.WriteLine($"[AuthService] {operation} non-critical: {ex.Message}");
            }
            catch
            {
            }
        }

        private async Task<(HttpStatusCode Status, string Body)> PostJsonAsync(string path, object payload, CancellationToken cancellationToken)
        {
            var url = _apiBaseUrl + path;
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (response.StatusCode, body);
        }
    }
}
