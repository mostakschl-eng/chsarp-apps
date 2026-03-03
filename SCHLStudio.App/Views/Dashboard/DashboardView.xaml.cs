using System;
using SCHLStudio.App.ViewModels.Windows;

using WpfDependencyObject = System.Windows.DependencyObject;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace SCHLStudio.App.Views.Dashboard
{
    public partial class DashboardView : WpfUserControl
    {
        public static readonly System.Windows.DependencyProperty UsernameProperty =
            System.Windows.DependencyProperty.Register(
                nameof(Username),
                typeof(string),
                typeof(DashboardView),
                new System.Windows.PropertyMetadata(string.Empty, OnUsernameChanged));

        private readonly DashboardLiveTrackingViewModel _viewModel;

        public string Username
        {
            get => (string)GetValue(UsernameProperty);
            set => SetValue(UsernameProperty, value);
        }

        public DashboardView()
        {
            InitializeComponent();
            _viewModel = new DashboardLiveTrackingViewModel();
            DataContext = _viewModel;
            _viewModel.SetUsername(Username);
        }

        private static void OnUsernameChanged(WpfDependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (d is DashboardView v)
                {
                    var nextUsername = e.NewValue as string;
                    v._viewModel.SetUsername(nextUsername);
                }
            }
            catch
            {
            }
        }

        private void DashFilterToday_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                _viewModel.SelectedDate = DateTime.Today;
                DashFilterToggle.IsChecked = false;
            }
            catch { }
        }

        private void DashFilterApply_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                DashFilterToggle.IsChecked = false;
            }
            catch { }
        }
    }
}
