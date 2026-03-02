using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class BreakDialog : Window
    {
        public string? SelectedReason { get; private set; }
        public string? Note { get; private set; }

        private readonly Action<string, string>? _onBreakStarted;

        public BreakDialog(IReadOnlyList<string> reasons, string? selectedReason, string? note, Action<string, string>? onBreakStarted = null)
        {
            InitializeComponent();

            _onBreakStarted = onBreakStarted;

            var items = (reasons ?? Array.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ReasonsList.ItemsSource = items;

            if (!string.IsNullOrWhiteSpace(selectedReason))
            {
                ReasonsList.SelectedItem = items.FirstOrDefault(x => string.Equals(x, selectedReason, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(selectedReason) && ReasonsList.SelectedItem is null)
            {
                CustomReasonTextBox.Text = selectedReason;
            }

            Loaded += (_, __) =>
            {
                try
                {
                    if (ReasonsList.SelectedItem is null)
                    {
                        ReasonsList.Focus();
                    }
                    else
                    {
                        CustomReasonTextBox.Focus();
                        CustomReasonTextBox.CaretIndex = CustomReasonTextBox.Text?.Length ?? 0;
                    }
                }
                catch
                {
                }
            };
        }

        private void ReasonsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void CustomReasonTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var custom = (CustomReasonTextBox.Text ?? string.Empty).Trim();
                SelectedReason = !string.IsNullOrWhiteSpace(custom)
                    ? custom
                    : (ReasonsList.SelectedItem?.ToString() ?? string.Empty).Trim();
                Note = custom;

                if (string.IsNullOrWhiteSpace(SelectedReason))
                {
                    return; // Prevent starting a break without a reason
                }

                _onBreakStarted?.Invoke(SelectedReason, Note);

                try
                {
                    SelectionView.Visibility = Visibility.Collapsed;
                    PausedView.Visibility = Visibility.Visible;
                    PausedReasonText.Text = SelectedReason;
                }
                catch { }

                // Do not close the dialog yet. We wait for the user to click Resume.
            }
            catch
            {
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
                Close();
            }
            catch
            {
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton_Click(sender, e);
        }

        private void WindowRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
            catch
            {
            }
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = true;
                Close();
            }
            catch
            {
            }
        }
    }
}
