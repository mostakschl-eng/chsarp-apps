using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SCHLStudio.App.Configuration;
using SCHLStudio.App.Services.Api;
using SCHLStudio.App.ViewModels.LiveTracking;
using SCHLStudio.App.ViewModels.Windows;
using SCHLStudio.App.Views.Dialogs;
using Application = System.Windows.Application;

namespace SCHLStudio.App.Views.Windows
{
    /// <summary>
    /// Layout: Header → Selection Bar → Paths Panel → Tracking Area
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(string username, string psVersion, string role)
        {
            InitializeComponent();

            _ = psVersion;

            DataContext = new AppShellContext(username, role);

            var r = (role ?? string.Empty).Trim();
            var isAdmin = string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase);
            var isSuperAdmin = string.Equals(r, "superadmin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, "super admin", StringComparison.OrdinalIgnoreCase);

            try
            {
                if (!isAdmin && !isSuperAdmin && string.Equals(r, "employee", StringComparison.OrdinalIgnoreCase))
                {
                    SearchTab.Style = (Style)FindResource("HiddenShellTabItemStyle");
                }
            }
            catch
            {
            }

            try
            {
                if (isAdmin)
                {
                    EnsureBackgroundLiveTrackingStarted(isAdmin, isSuperAdmin);

                    DashboardTab.Style = (Style)FindResource("HiddenShellTabItemStyle");
                    SettingsTab.Style = (Style)FindResource("HiddenShellTabItemStyle");

                    try
                    {
                        FilesTab.ClearValue(TabItem.StyleProperty);
                        SearchTab.ClearValue(TabItem.StyleProperty);
                        LiveTrackingTab.ClearValue(TabItem.StyleProperty);
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (MainTabs is not null)
                        {
                            MainTabs.Items.Remove(LiveTrackingTab);
                            MainTabs.Items.Remove(SearchTab);
                            MainTabs.Items.Remove(FilesTab);

                            MainTabs.Items.Add(LiveTrackingTab);
                            MainTabs.Items.Add(SearchTab);
                            MainTabs.Items.Add(FilesTab);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (MainTabs is not null)
                        {
                            MainTabs.SelectedItem = LiveTrackingTab;
                        }
                    }
                    catch
                    {
                    }

                    return;
                }
            }
            catch
            {
            }

            try
            {
                var hideLiveTracking = !isAdmin && !isSuperAdmin;

                if (hideLiveTracking)
                {
                    LiveTrackingTab.Style = (Style)FindResource("HiddenShellTabItemStyle");
                }
            }
            catch
            {
            }

            EnsureBackgroundLiveTrackingStarted(isAdmin, isSuperAdmin);

            try
            {
                if (Application.Current is App app)
                {
                    app.SetCurrentMainWindow(this);
                }
            }
            catch
            {
            }

        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                LiveTrackingViewModel.Shared.StopTracking();
            }
            catch
            {
            }

            base.OnClosed(e);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                try
                {
                    LiveTrackingViewModel.Shared.StopTracking();
                }
                catch
                {
                }

                if (Application.Current is not App app)
                {
                    return;
                }

                var confirm = false;
                try
                {
                    var dlg = new ConfirmLogoutWindow
                    {
                        Owner = this
                    };
                    confirm = dlg.ShowDialog() == true;
                }
                catch
                {
                    confirm = true;
                }

                if (!confirm)
                {
                    return;
                }

                try
                {
                    var sessionId = AppConfig.CurrentTrackerSessionId;
                    if (!string.IsNullOrWhiteSpace(sessionId) && app.ServiceProvider is not null)
                    {
                        var apiClient = app.ServiceProvider.GetService(typeof(IApiClient)) as IApiClient;
                        if (apiClient is not null)
                        {
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            {
                                try
                                {
                                    await apiClient.LogoutAsync(sessionId).ConfigureAwait(false);
                                }
                                catch
                                {
                                }
                            });
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    AppConfig.SetCurrentAppUser(null);
                    AppConfig.SetCurrentTrackerSession(null, null);
                }
                catch
                {
                }

                app.BeginLogout();
                app.ShowLoginWindow();

                try
                {
                    Close();
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        private static void EnsureBackgroundLiveTrackingStarted(bool isAdmin, bool isSuperAdmin)
        {
            try
            {
                if (!isAdmin && !isSuperAdmin)
                {
                    return;
                }

                LiveTrackingViewModel.Shared.StartTracking();
            }
            catch
            {
            }
        }
    }
}
