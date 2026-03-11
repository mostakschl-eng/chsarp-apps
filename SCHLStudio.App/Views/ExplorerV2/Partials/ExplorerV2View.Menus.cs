using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SCHLStudio.App.Configuration;
using SCHLStudio.App.Views.ExplorerV2.Models;
using SCHLStudio.App.Views.ExplorerV2.Services;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private void UpdateTaskMenuActiveHighlight()
        {
            try
            {
                var primary = _cachedPrimaryBrush;
                var normal = _cachedTextMainBrush;

                foreach (var mi in TaskMenu.Items.OfType<MenuItem>())
                {
                    var header = (mi.Header?.ToString() ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(header) && _activeJobTasks.Contains(header) && primary is not null)
                    {
                        mi.Foreground = primary;
                    }
                    else if (normal is not null)
                    {
                        mi.Foreground = normal;
                    }
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void BuildOpenWithMenu()
        {
            try
            {
                OpenWithMenu.Items.Clear();

                foreach (var opt in _openWithOptions)
                {
                    var name = (opt ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var mi = new MenuItem
                    {
                        Header = name,
                        IsCheckable = false,
                        StaysOpenOnClick = false
                    };

                    mi.Click += (_, _) =>
                    {
                        try
                        {
                            OpenSelectedFilesWithOpenWith(name);
                        }
                        catch (Exception ex_safe_log)
                        {
                            LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
                        }
                    };
                    OpenWithMenu.Items.Add(mi);
                }

                _vm.SelectedOpenWith = null;
                _vm.OpenWithButtonText = "Open With";
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void SelectSingleOpenWith(MenuItem checkedItem, string openWith)
        {
            try
            {
                foreach (var it in OpenWithMenu.Items.OfType<MenuItem>())
                {
                    if (!ReferenceEquals(it, checkedItem))
                    {
                        it.IsChecked = false;
                    }
                }

                _vm.SelectedOpenWith = openWith;
                _vm.OpenWithButtonText = "Open With";

                try
                {
                    OpenSelectedFilesWithOpenWith(openWith);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void OpenSelectedFilesWithOpenWith(string openWith)
        {
            try
            {
                var selected = new List<string>();

                try
                {
                    selected = _vm.SelectedFiles
                        .OfType<SelectedFileRow>()
                        .Select(x => (x?.FullPath ?? string.Empty).Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
                }

                if (selected.Count == 0)
                {
                    try
                    {
                        System.Windows.MessageBox.Show(
                            "Select files first.",
                            "SCHL App",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex_safe_log)
                    {
                        LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
                    }

                    return;
                }

                var key = MapOpenWithToPhotoshopKey(openWith);
                if (string.IsNullOrWhiteSpace(key))
                {
                    return;
                }

                var ps = new PhotoshopLauncher();

                ps.OpenFiles(key, selected);
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private static string MapOpenWithToPhotoshopKey(string openWith)
        {
            try
            {
                var s = (openWith ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return PhotoshopLauncher.AutoKey;
                }

                if (string.Equals(s, "Photoshop 26", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoshopLauncher.Ps2026Key;
                }

                if (string.Equals(s, "Photoshop 25", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoshopLauncher.Ps2025Key;
                }

                if (string.Equals(s, "Photoshop CC", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoshopLauncher.PsCcKey;
                }

                return PhotoshopLauncher.AutoKey;
            }
            catch
            {
                return PhotoshopLauncher.AutoKey;
            }
        }

        private void BuildWorkTypeMenu()
        {
            try
            {
                WorkTypeMenu.Items.Clear();
                foreach (var wt in _workTypes)
                {
                    var mi = new MenuItem
                    {
                        Header = wt,
                        IsCheckable = true,
                        StaysOpenOnClick = false
                    };
                    mi.Checked += (_, _) => SelectSingleWorkType(mi, wt);
                    mi.Unchecked += (_, _) => WorkTypeMenuItem_Unchecked(mi, wt);
                    WorkTypeMenu.Items.Add(mi);
                }
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void WorkTypeMenuItem_Unchecked(MenuItem uncheckedItem, string workType)
        {
            try
            {
                var anyChecked = false;
                try
                {
                    anyChecked = WorkTypeMenu.Items.OfType<MenuItem>().Any(x => x.IsChecked);
                }
                catch (Exception ex_safe_log)
                {
                    LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
                }

                if (anyChecked)
                {
                    return;
                }

                var current = (_vm.WorkTypeButtonText ?? string.Empty);
                // Reset the button whenever it still holds a work-type value (i.e. isn't already
                // showing the default label).  Both previous branches did exactly the same thing,
                // so a single guard is all that is needed.
                if (!string.IsNullOrWhiteSpace(current) &&
                    !string.Equals(current, "Work Type", StringComparison.OrdinalIgnoreCase))
                {
                    _vm.WorkTypeButtonText = "Work Type";
                    _vm.SelectedWorkType = null;
                }

                UpdateSelectedFilesMetaText();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void SelectSingleWorkType(MenuItem checkedItem, string workType)
        {
            try
            {
                foreach (var it in WorkTypeMenu.Items.OfType<MenuItem>())
                {
                    if (!ReferenceEquals(it, checkedItem))
                    {
                        it.IsChecked = false;
                    }
                }

                _vm.SelectedWorkType = workType;
                _vm.WorkTypeButtonText = workType;
                UpdateSelectedFilesMetaText();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void BuildTaskMenu()
        {
            try
            {
                TaskMenu.Items.Clear();

                var cfg = (System.Windows.Application.Current as SCHLStudio.App.App)
                    ?.ServiceProvider
                    ?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    as Microsoft.Extensions.Configuration.IConfiguration;

                var categories = new List<string>();
                try
                {
                    foreach (var child in (cfg?.GetSection("Categories")?.GetSection("Default")?.GetChildren()
                                 ?? Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>()))
                    {
                        var v = (child?.Value ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            categories.Add(v);
                        }
                    }
                }
                catch
                {
                    categories.Clear();
                }

                foreach (var cat in categories.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var name = (cat ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var mi = new MenuItem
                    {
                        Header = name,
                        IsCheckable = true,
                        StaysOpenOnClick = true
                    };
                    mi.Checked += (_, _) =>
                    {
                        UpdateTaskButtonHeader();
                        UpdateTaskMenuActiveHighlight();
                    };
                    mi.Unchecked += (_, _) =>
                    {
                        UpdateTaskButtonHeader();
                        UpdateTaskMenuActiveHighlight();
                    };
                    TaskMenu.Items.Add(mi);
                }

                UpdateTaskButtonHeader();
                UpdateTaskMenuActiveHighlight();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void UpdateTaskButtonHeader()
        {
            try
            {
                var selected = TaskMenu.Items
                    .OfType<MenuItem>()
                    .Where(i => i.IsChecked)
                    .Select(i => (i.Header?.ToString() ?? string.Empty).Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (selected.Count == 0)
                {
                    TaskButton.Content = "Task";
                    TaskSelectedChips.ItemsSource = Array.Empty<string>();
                    return;
                }

                TaskButton.Content = "Task";
                TaskSelectedChips.ItemsSource = selected;
                UpdateSelectedFilesMetaText();
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void TaskControlContainer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount < 1)
                {
                    return;
                }

                TaskButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void WorkTypeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ctx = WorkTypeButton.ContextMenu;
                if (ctx is null)
                {
                    return;
                }

                ctx.PlacementTarget = WorkTypeButton;
                ctx.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                ctx.HorizontalOffset = 0;
                ctx.VerticalOffset = 6;
                ctx.MinWidth = Math.Max(240, WorkTypeButton.ActualWidth);
                ctx.IsOpen = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void TaskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ctx = TaskButton.ContextMenu;
                if (ctx is null)
                {
                    return;
                }

                ctx.PlacementTarget = TaskControlContainer;
                ctx.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                ctx.HorizontalOffset = 0;
                ctx.VerticalOffset = 6;
                ctx.MinWidth = Math.Max(260, TaskControlContainer.ActualWidth);
                ctx.IsOpen = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

        private void ExecuteOpenWithWorkflowFromVm()
        {
            try
            {
                var ctx = OpenWithButton.ContextMenu;
                if (ctx is null)
                {
                    return;
                }

                ctx.PlacementTarget = OpenWithButton;
                ctx.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                ctx.HorizontalOffset = 0;
                ctx.VerticalOffset = -10;
                ctx.MinWidth = Math.Max(0, OpenWithButton.ActualWidth);
                ctx.IsOpen = true;
            }
            catch (Exception ex_safe_log)
            {
                LogSuppressedError("ExplorerV2View.Menus", ex_safe_log);
            }
        }

    }
}
