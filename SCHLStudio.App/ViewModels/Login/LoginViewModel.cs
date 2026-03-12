using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SCHLStudio.App.ViewModels.Base;
using SCHLStudio.App.Services.Api;
using MessageBox = System.Windows.MessageBox;

namespace SCHLStudio.App.ViewModels.Windows
{
    /// <summary>
    /// LoginViewModel - Matches Python ui/login_window.py 3-step flow
    /// Step 1: Username check
    /// Step 2a: Password (if user has password) + Photoshop version
    /// Step 2b: Set password (if user has no password)
    /// Step 3: Photoshop version selection (after password setup)
    /// </summary>
    public class LoginViewModel : ViewModelBase
    {
        private readonly IApiClient _apiClient;
        private CancellationTokenSource? _operationCts;
        private int _operationId;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string? _sessionId;
        private string? _userId;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _selectedVersion = "Select Version...";
        private string _currentStep = "username";
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        private string _greeting = string.Empty;
        private string _loginRole = "Employee";

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string SelectedVersion
        {
            get => _selectedVersion;
            set => SetProperty(ref _selectedVersion, value);
        }

        public string CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    OnPropertyChanged(nameof(IsUsernameStep));
                    OnPropertyChanged(nameof(IsPasswordStep));
                    OnPropertyChanged(nameof(IsSetPasswordStep));
                    OnPropertyChanged(nameof(IsPhotoshopSelectionStep));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string Greeting
        {
            get => _greeting;
            set => SetProperty(ref _greeting, value);
        }

        public string? SessionId
        {
            get => _sessionId;
            private set => SetProperty(ref _sessionId, value);
        }

        public string? UserId
        {
            get => _userId;
            private set => SetProperty(ref _userId, value);
        }

        // Step visibility properties
        public bool IsUsernameStep => CurrentStep == "username";
        public bool IsPasswordStep => CurrentStep == "password";
        public bool IsSetPasswordStep => CurrentStep == "set_password";
        public bool IsPhotoshopSelectionStep => CurrentStep == "photoshop_selection";

        // Commands
        public AsyncRelayCommand CheckUsernameCommand { get; }
        public AsyncRelayCommand LoginCommand { get; }
        public AsyncRelayCommand CreatePasswordCommand { get; }
        public RelayCommand CompleteLoginCommand { get; }
        public RelayCommand BackToUsernameCommand { get; }

        // Events
        public event Action<string, string, string>? LoginSuccessful; // username, psVersion, role

        public LoginViewModel(IApiClient apiClient)
        {
            _apiClient = apiClient;

            // Initialize commands
            CheckUsernameCommand = new AsyncRelayCommand(async _ => await CheckUsernameAsync(), _ => !IsLoading);
            LoginCommand = new AsyncRelayCommand(async _ => await LoginAsync(), _ => !IsLoading);
            CreatePasswordCommand = new AsyncRelayCommand(async _ => await CreatePasswordAsync(), _ => !IsLoading);
            CompleteLoginCommand = new RelayCommand(_ => CompleteLogin(), _ => !IsLoading);
            BackToUsernameCommand = new RelayCommand(_ => BackToUsername());

            // Set time-based greeting
            UpdateGreeting();
        }

        private void BackToUsername()
        {
            try
            {
                CancelCurrentOperation();
                Password = string.Empty;
                SessionId = null;
                UserId = null;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
                IsLoading = false;
                StatusMessage = string.Empty;
                CurrentStep = "username";
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(BackToUsername), ex);
            }
        }

        private void UpdateGreeting()
        {
            var hour = DateTime.Now.Hour;
            Greeting = (hour >= 7 && hour < 15) ? "Good Morning" : "Good Evening";
        }

        private async Task CheckUsernameAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Please enter username", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var normalizedUsername = Username.Trim();
            Username = normalizedUsername;

            var operation = BeginOperation("Checking...");

            try
            {
                var response = await _apiClient.CheckUserTypedAsync(Username, operation.Token);
                operation.Token.ThrowIfCancellationRequested();

                if (!response.Exists)
                {
                    MessageBox.Show("Username not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (response.PasswordRequired)
                {
                    CurrentStep = "password";
                }
                else
                {
                    CurrentStep = "set_password";
                }

                UpdateGreeting();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndOperation(operation);
            }
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter password", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var normalizedUsername = Username.Trim();
            var normalizedPassword = Password.Trim();
            Username = normalizedUsername;
            Password = normalizedPassword;

            var operation = BeginOperation("Logging in...");

            try
            {
                var response = await _apiClient.LoginTypedAsync(Username, Password, operation.Token);
                operation.Token.ThrowIfCancellationRequested();

                if (response.PasswordSetupRequired)
                {
                    CurrentStep = "set_password";
                    return;
                }

                var isSuccess = response.Success || response.Valid;

                if (isSuccess)
                {
                    ApplyLoginIdentity(response);
                    var role = string.IsNullOrWhiteSpace(_loginRole) ? "Employee" : _loginRole;
                    var psVersion = "v26";
                    LoginSuccessful?.Invoke(Username, psVersion, role);
                }
                else
                {
                    var message = string.IsNullOrWhiteSpace(response.Message) ? "Invalid password" : response.Message;
                    MessageBox.Show(message, "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndOperation(operation);
            }
        }

        private async Task CreatePasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("Please enter a password", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var normalizedUsername = Username.Trim();
            var normalizedNewPassword = NewPassword.Trim();
            var normalizedConfirmPassword = ConfirmPassword.Trim();
            Username = normalizedUsername;
            NewPassword = normalizedNewPassword;
            ConfirmPassword = normalizedConfirmPassword;

            if (NewPassword.Length < 4)
            {
                MessageBox.Show("Password must be at least 4 characters", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("Passwords do not match", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var operation = BeginOperation("Creating...");

            try
            {
                var response = await _apiClient.SetPasswordTypedAsync(Username, NewPassword, operation.Token);
                operation.Token.ThrowIfCancellationRequested();

                if (response.Success)
                {
                    var loginResponse = await _apiClient.LoginTypedAsync(Username, NewPassword, operation.Token);
                    operation.Token.ThrowIfCancellationRequested();

                    if (loginResponse.Success || loginResponse.Valid)
                    {
                        ApplyLoginIdentity(loginResponse);
                        var psVersion = "v26";
                        var role = string.IsNullOrWhiteSpace(_loginRole) ? "Employee" : _loginRole;
                        LoginSuccessful?.Invoke(Username, psVersion, role);
                    }
                    else
                    {
                        MessageBox.Show("Please try logging in again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        CurrentStep = "username";
                    }
                }
                else
                {
                    var message = string.IsNullOrWhiteSpace(response.Message) ? "Failed to create password" : response.Message;
                    MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndOperation(operation);
            }
        }

        private void CompleteLogin()
        {
            var psVersion = "v26";
            var role = string.IsNullOrWhiteSpace(_loginRole) ? "Employee" : _loginRole;
            LoginSuccessful?.Invoke(Username, psVersion, role);
        }

        private void ApplyLoginIdentity(ApiLoginResult response)
        {
            _loginRole = string.IsNullOrWhiteSpace(response.Role) ? "Employee" : response.Role;

            SessionId = response.SessionId;
            UserId = response.UserId;

            var serverUsername = (response.Username ?? Username ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(serverUsername))
            {
                Username = serverUsername;
            }

            var displayName = !string.IsNullOrWhiteSpace(response.DisplayName)
                ? response.DisplayName
                : Username;
            Configuration.AppConfig.SetCurrentDisplayName(displayName);

            // Store activeWork for ExplorerV2 to consume on initialization
            Configuration.AppConfig.SetPendingActiveWork(response.ActiveWork);
        }

        private (CancellationTokenSource Cts, CancellationToken Token, int OperationId) BeginOperation(string status)
        {
            try
            {
                _operationCts?.Cancel();
            }
            catch (Exception ex)
            {
                LogNonCritical("BeginOperation.CancelPrevious", ex);
            }

            try
            {
                _operationCts?.Dispose();
            }
            catch (Exception ex)
            {
                LogNonCritical("BeginOperation.DisposePrevious", ex);
            }

            var cts = new CancellationTokenSource();
            _operationCts = cts;

            var operationId = Interlocked.Increment(ref _operationId);
            IsLoading = true;
            StatusMessage = status;

            return (cts, cts.Token, operationId);
        }

        private void EndOperation((CancellationTokenSource Cts, CancellationToken Token, int OperationId) operation)
        {
            try
            {
                if (ReferenceEquals(_operationCts, operation.Cts))
                {
                    _operationCts = null;
                }

                operation.Cts.Dispose();
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(EndOperation), ex);
            }

            if (operation.OperationId == _operationId)
            {
                IsLoading = false;
                StatusMessage = string.Empty;
            }
        }

        private void CancelCurrentOperation()
        {
            try
            {
                _operationCts?.Cancel();
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(CancelCurrentOperation), ex);
            }
        }

        private void UpdateCommandStates()
        {
            try
            {
                CheckUsernameCommand?.RaiseCanExecuteChanged();
                LoginCommand?.RaiseCanExecuteChanged();
                CreatePasswordCommand?.RaiseCanExecuteChanged();
                CompleteLoginCommand?.RaiseCanExecuteChanged();
                BackToUsernameCommand?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                LogNonCritical(nameof(UpdateCommandStates), ex);
            }
        }

        private static void LogNonCritical(string operation, Exception ex)
        {
            try
            {
                Debug.WriteLine($"[LoginViewModel] {operation} non-critical: {ex.Message}");
            }
            catch
            {
            }
        }
    }
}
