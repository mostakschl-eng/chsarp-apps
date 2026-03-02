using System;
using System.Windows;

namespace SCHLStudio.App.Views.Dialogs
{
    public partial class CommentDialogWindow : Window
    {
        public string CommentText { get; private set; } = string.Empty;

        public CommentDialogWindow(string? initialText = null)
        {
            InitializeComponent();

            try
            {
                CommentInput.Text = (initialText ?? string.Empty).Trim();
                CommentInput.CaretIndex = CommentInput.Text.Length;
                CommentInput.Focus();
            }
            catch
            {
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommentText = (CommentInput.Text ?? string.Empty).Trim();
            }
            catch
            {
                CommentText = string.Empty;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (DialogResult != true)
                {
                    CommentText = (CommentInput.Text ?? string.Empty).Trim();
                }
            }
            catch
            {
            }

            base.OnClosed(e);
        }
    }
}
