using System.Windows;
using SCHLStudio.App.Configuration;
using SCHLStudio.App.Services.Diagnostics;
using SCHLStudio.App.ViewModels.Windows;
using SCHLStudio.App.Views.Windows;
using SCHLStudio.App.Services.Api;
using Application = System.Windows.Application;

namespace SCHLStudio.App.Views.Login
{
    /// <summary>
    /// Login Window - Matches Python ui/login_window.py
    /// 3-step flow: Username → Password/Setup → Photoshop Selection
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        private readonly System.IServiceProvider _serviceProvider;

        public LoginWindow(IApiClient apiClient, System.IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;

            _viewModel = new LoginViewModel(apiClient);
            DataContext = _viewModel;

            PasswordInput.PasswordChanged += (_, _) => _viewModel.Password = PasswordInput.Password;
            NewPasswordInput.PasswordChanged += (_, _) => _viewModel.NewPassword = NewPasswordInput.Password;
            ConfirmPasswordInput.PasswordChanged += (_, _) => _viewModel.ConfirmPassword = ConfirmPasswordInput.Password;

            // Subscribe to login success event
            _viewModel.LoginSuccessful += OnLoginSuccessful;
        }

        private void OnLoginSuccessful(string username, string psVersion, string role)
        {
            try
            {
                AppConfig.SetCurrentAppUser(username);
                AppConfig.SetCurrentAppRole(role);
                AppConfig.SetCurrentTrackerSession(_viewModel.SessionId, _viewModel.UserId);
            }
            catch
            {
            }

            var mainWindow = new MainWindow(username, psVersion, role);
            mainWindow.Show();

            try
            {
                if (Application.Current is App app)
                {
                    app.SetCurrentMainWindow(mainWindow);
                }
            }
            catch (System.Exception ex)
            {
                try
                {
                    AppDataLog.LogError(
                        area: "App",
                        operation: "LoginWindow.OnLoginSuccessful.SetCurrentMainWindow",
                        ex: ex);
                }
                catch
                {
                }
            }

            // Close login window
            this.Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.LoginSuccessful -= OnLoginSuccessful;

            if (Application.Current?.MainWindow == this || Application.Current?.MainWindow == null)
            {
                Application.Current?.Shutdown();
            }
        }
    }
}
