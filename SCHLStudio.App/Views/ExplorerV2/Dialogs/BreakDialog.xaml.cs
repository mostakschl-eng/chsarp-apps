using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class BreakDialog : Window
    {
        public string? SelectedReason { get; private set; }
        public string? Note { get; private set; }

        private readonly Action<string, string>? _onBreakStarted;
        private DispatcherTimer? _pauseTimer;
        private DateTime _pauseStartedAt;

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
                var selected = (ReasonsList.SelectedItem?.ToString() ?? string.Empty).Trim();
                var custom = (CustomReasonTextBox.Text ?? string.Empty).Trim();

                // Combine: if both selected and custom, join them
                if (!string.IsNullOrWhiteSpace(selected) && !string.IsNullOrWhiteSpace(custom))
                    SelectedReason = $"{selected} - {custom}";
                else if (!string.IsNullOrWhiteSpace(custom))
                    SelectedReason = custom;
                else
                    SelectedReason = selected;

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

                    // Hide close button so user cannot accidentally dismiss
                    CloseBtn.Visibility = Visibility.Collapsed;

                    // Start pause timer
                    _pauseStartedAt = DateTime.Now;
                    PauseTimerText.Text = "00:00";
                    PauseTimerText.Visibility = Visibility.Visible;
                    _pauseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _pauseTimer.Tick += (_, __) =>
                    {
                        try
                        {
                            var elapsed = DateTime.Now - _pauseStartedAt;
                            PauseTimerText.Text = elapsed.TotalHours >= 1
                                ? elapsed.ToString(@"hh\:mm\:ss")
                                : elapsed.ToString(@"mm\:ss");
                        }
                        catch { }
                    };
                    _pauseTimer.Start();
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
                _pauseTimer?.Stop();
                _pauseTimer = null;
                DialogResult = true;
                Close();
            }
            catch
            {
            }
        }
    }
}
