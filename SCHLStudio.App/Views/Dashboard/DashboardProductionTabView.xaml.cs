namespace SCHLStudio.App.Views.Dashboard
{
    public partial class DashboardProductionTabView : System.Windows.Controls.UserControl
    {
        public DashboardProductionTabView()
        {
            InitializeComponent();
        }

        private void CopyFileNameButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (sender is not System.Windows.FrameworkElement fe)
                {
                    return;
                }

                var text = (fe.Tag as string ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    System.Windows.Clipboard.SetText(text);
                }

                e.Handled = true;
            }
            catch
            {
            }
        }
    }
}
