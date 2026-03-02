using System.Threading;
using System.Threading.Tasks;

namespace SCHLStudio.App.Services.Api
{
    public interface IApiClient
    {
        bool IsAuthenticated { get; }

        Task<ApiLoginResult> LoginTypedAsync(string username, string password, CancellationToken cancellationToken = default);
        Task<ApiSetPasswordResult> SetPasswordTypedAsync(string username, string password, CancellationToken cancellationToken = default);
        Task<ApiCheckUserResult> CheckUserTypedAsync(string username, CancellationToken cancellationToken = default);
        Task<ApiSearchFileResult> SearchFileTypedAsync(string query, string? clientCode = null, CancellationToken cancellationToken = default);
        Task<bool> ReportFileTypedAsync(ApiReportFileRequest request, CancellationToken cancellationToken = default);

        Task<string> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
        Task<string> SetPasswordAsync(string username, string password, CancellationToken cancellationToken = default);
        Task<string> CheckUserAsync(string username, CancellationToken cancellationToken = default);
        Task<string> GetJobListAsync(string? clientCode = null, CancellationToken cancellationToken = default);
        Task<string> SearchFileAsync(string query, string? clientCode = null, CancellationToken cancellationToken = default);
        Task<string> ReportFileAsync(ApiReportFileRequest request, CancellationToken cancellationToken = default);
        Task<string> GetDashboardTodayAsync(string? username = null, string? date = null, CancellationToken cancellationToken = default);

        Task<bool> LogoutAsync(string sessionId, CancellationToken cancellationToken = default);

        Task<bool> WaitForSyncAsync(int timeoutSeconds = 10, CancellationToken cancellationToken = default);
        void Stop();
    }
}
