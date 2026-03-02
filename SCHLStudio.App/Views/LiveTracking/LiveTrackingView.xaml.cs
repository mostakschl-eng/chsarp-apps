using System;
using SCHLStudio.App.ViewModels.LiveTracking;
using SCHLStudio.App.ViewModels.Windows;
using System.Windows;

namespace SCHLStudio.App.Views.LiveTracking
{
    public partial class LiveTrackingView : System.Windows.Controls.UserControl
    {
        private LiveTrackingViewModel? _viewModel;

        public LiveTrackingView()
        {
            InitializeComponent();
            
            _viewModel = LiveTrackingViewModel.Shared;
            DataContext = _viewModel;
            
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Only start socket connection if the user has an allowed role (admin/superadmin).
            // This prevents the connect-then-disconnect cycle for unauthorized users.
            if (!IsLiveTrackingAllowed())
                return;

            _viewModel?.StartTracking();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        private bool IsLiveTrackingAllowed()
        {
            try
            {
                var window = Window.GetWindow(this);
                if (window?.DataContext is AppShellContext ctx)
                {
                    var role = (ctx.CurrentRole ?? string.Empty).Trim();
                    return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(role, "superadmin", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(role, "super admin", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }
            // If we can't determine the role, don't start the connection.
            return false;
        }
    }
}
