using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private void GuidelineButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ctx = GuidelineButton.ContextMenu;
                if (ctx is null)
                {
                    return;
                }

                try
                {
                    BuildGuidelineMenuMock();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Guideline", ex_safe_log);
                }

                ctx.PlacementTarget = GuidelineButton;
                ctx.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                ctx.HorizontalOffset = 0;
                ctx.VerticalOffset = 6;
                ctx.MinWidth = 0;
                ctx.IsOpen = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Guideline", ex_safe_log);
            }
        }

        private void BuildGuidelineMenuMock()
        {
            try
            {
                if (GuidelineMenu is null)
                {
                    return;
                }

                GuidelineMenu.Items.Clear();

                var plainStyle = TryFindResource("BreakPlainMenuItemStyle") as Style;

                GuidelineMenu.Items.Add(new MenuItem
                {
                    Header = "1) Retouch neck side",
                    IsEnabled = false,
                    Style = plainStyle
                });

                GuidelineMenu.Items.Add(new MenuItem
                {
                    Header = "2) Perfectly match",
                    IsEnabled = false,
                    Style = plainStyle
                });

                GuidelineMenu.Items.Add(new MenuItem
                {
                    Header = "3) Need good quality",
                    IsEnabled = false,
                    Style = plainStyle
                });

                GuidelineMenu.Items.Add(new Separator());

                var link = string.Empty;
                var mi = new MenuItem
                {
                    Header = "No guideline link available",
                    IsEnabled = false,
                    StaysOpenOnClick = false,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Style = plainStyle
                };
                GuidelineMenu.Items.Add(mi);
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Guideline", ex_safe_log);
            }
        }

        private void OpenGuidelineLink(string url)
        {
            try
            {
                var u = (url ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(u))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo(u) { UseShellExecute = true });
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Guideline", ex_safe_log);
            }
        }
    }
}
