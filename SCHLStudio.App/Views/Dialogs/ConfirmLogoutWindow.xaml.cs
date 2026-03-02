using System.Windows;

namespace SCHLStudio.App.Views.Dialogs
{
    public partial class ConfirmLogoutWindow : Window
    {
        public ConfirmLogoutWindow()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = true;
            }
            catch
            {
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
            }
            catch
            {
                Close();
            }
        }
    }
}
