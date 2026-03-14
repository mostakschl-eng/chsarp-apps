using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SCHLStudio.App.Views.ExplorerV2.Models;

namespace SCHLStudio.App.Views.ExplorerV2
{
    public partial class ExplorerV2View
    {
        private sealed class RubberbandAdorner : Adorner
        {
            private Rect _rect;

            public RubberbandAdorner(UIElement adornedElement) : base(adornedElement)
            {
                IsHitTestVisible = false;
            }

            public void Update(Rect rect)
            {
                _rect = rect;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                if (_rect.Width <= 0 || _rect.Height <= 0)
                {
                    return;
                }

                var stroke = new System.Windows.Media.Pen(
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 126, 166, 65)),
                    1);
                stroke.Freeze();

                var fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 126, 166, 65));
                fill.Freeze();

                drawingContext.DrawRectangle(fill, stroke, _rect);
            }
        }

        private void FilesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (FilesListView is null)
                {
                    return;
                }

                _rubberbandStart = e.GetPosition(FilesListView);

                var original = e.OriginalSource as DependencyObject;

                if (FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(original) is not null)
                {
                    return;
                }

                var item = FindAncestor<System.Windows.Controls.ListViewItem>(original);
                if (item is not null)
                {
                    _filesDragStart = e.GetPosition(FilesListView);
                    _isFilesDragArmed = true;

                    if (item.IsSelected && FilesListView.SelectedItems.Count > 1)
                    {
                        e.Handled = true;
                    }

                    return;
                }

                _isFilesDragArmed = false;

                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                {
                    FilesListView.SelectedItems.Clear();
                }

                _isRubberbandSelecting = true;
                _lastRubberbandSelectionUtc = DateTime.MinValue;
                _lastRubberbandRect = Rect.Empty;
                FilesListView.CaptureMouse();

                _rubberbandLayer = AdornerLayer.GetAdornerLayer(FilesListView);
                if (_rubberbandLayer is not null)
                {
                    _rubberbandAdorner ??= new RubberbandAdorner(FilesListView);
                    _rubberbandLayer.Add(_rubberbandAdorner);
                    _rubberbandAdorner.Update(new Rect(_rubberbandStart, _rubberbandStart));
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogSuppressedError("FilesListView_PreviewMouseLeftButtonDown", ex);
            }
        }

        private void FilesListView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (_isFilesDragArmed && !_isRubberbandSelecting && FilesListView is not null && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(FilesListView);
                    var dx = Math.Abs(pos.X - _filesDragStart.X);
                    var dy = Math.Abs(pos.Y - _filesDragStart.Y);
                    if (dx > SystemParameters.MinimumHorizontalDragDistance || dy > SystemParameters.MinimumVerticalDragDistance)
                    {
                        var selected = FilesListView.SelectedItems.OfType<SCHLStudio.App.Views.ExplorerV2.Models.FileTileItem>().ToList();
                        if (selected.Count == 0)
                        {
                            var hit = e.OriginalSource as DependencyObject;
                            var lvi = FindAncestor<System.Windows.Controls.ListViewItem>(hit);
                            if (lvi?.DataContext is SCHLStudio.App.Views.ExplorerV2.Models.FileTileItem ft)
                            {
                                selected.Add(ft);
                            }
                        }

                        if (selected.Count > 0)
                        {
                            _isFilesDragArmed = false;
                            var paths = selected
                                .Select(x => x.FullPath)
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToArray();

                            if (paths.Length > 0)
                            {
                                var data = new System.Windows.DataObject();
                                data.SetData("SCHLStudio_InternalDrag", true);
                                data.SetData(System.Windows.DataFormats.FileDrop, paths);
                                try
                                {
                                    System.Windows.DragDrop.DoDragDrop(FilesListView, data, System.Windows.DragDropEffects.Copy);
                                }
                                catch (Exception ex)
                                {
                                    _isFilesDragArmed = false;
                                    LogSuppressedError("DoDragDrop", ex);
                                }
                                return;
                            }
                        }
                    }
                }

                if (!_isRubberbandSelecting || FilesListView is null)
                {
                    return;
                }

                if (e.LeftButton != MouseButtonState.Pressed)
                {
                    return;
                }

                var current = e.GetPosition(FilesListView);
                var rect = NormalizeRect(_rubberbandStart, current);
                _rubberbandAdorner?.Update(rect);

                var now = DateTime.UtcNow;
                var sinceLastMs = (now - _lastRubberbandSelectionUtc).TotalMilliseconds;
                var sameRect = Math.Abs(rect.X - _lastRubberbandRect.X) < 1
                    && Math.Abs(rect.Y - _lastRubberbandRect.Y) < 1
                    && Math.Abs(rect.Width - _lastRubberbandRect.Width) < 1
                    && Math.Abs(rect.Height - _lastRubberbandRect.Height) < 1;
                if (sameRect || sinceLastMs < 24)
                {
                    return;
                }

                _lastRubberbandSelectionUtc = now;
                _lastRubberbandRect = rect;

                UpdateSelectionByRect(rect);
            }
            catch (Exception ex)
            {
                LogSuppressedError("FilesListView_PreviewMouseMove", ex);
            }
        }

        private void FilesListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                _isFilesDragArmed = false;

                if (!_isRubberbandSelecting)
                {
                    return;
                }

                _isRubberbandSelecting = false;
                try
                {
                    FilesListView?.ReleaseMouseCapture();
                }
                catch (Exception ex)
                {
                    LogSuppressedError("FilesListView_PreviewMouseLeftButtonUp.ReleaseMouseCapture", ex);
                }

                if (_rubberbandLayer is not null && _rubberbandAdorner is not null)
                {
                    try
                    {
                        _rubberbandLayer.Remove(_rubberbandAdorner);
                    }
                    catch (Exception ex)
                    {
                        LogSuppressedError("FilesListView_PreviewMouseLeftButtonUp.RemoveAdorner", ex);
                    }
                }

                _rubberbandLayer = null;
                _lastRubberbandRect = Rect.Empty;
            }
            catch (Exception ex)
            {
                LogSuppressedError("FilesListView_PreviewMouseLeftButtonUp", ex);
            }
        }

        private void UpdateSelectionByRect(Rect rect)
        {
            try
            {
                if (FilesListView is null)
                {
                    return;
                }

                var add = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

                var targetItems = new HashSet<object>();

                for (var i = 0; i < FilesListView.Items.Count; i++)
                {
                    if (FilesListView.ItemContainerGenerator.ContainerFromIndex(i) is not System.Windows.Controls.ListViewItem lvi)
                    {
                        continue;
                    }

                    var bounds = GetBoundsRelativeTo(lvi, FilesListView);
                    if (rect.IntersectsWith(bounds))
                    {
                        targetItems.Add(FilesListView.Items[i]);
                    }
                }

                var currentlySelected = FilesListView.SelectedItems.Cast<object>().ToHashSet();

                if (!add)
                {
                    for (var i = FilesListView.SelectedItems.Count - 1; i >= 0; i--)
                    {
                        var selectedItem = FilesListView.SelectedItems[i];
                        if (selectedItem is null || !targetItems.Contains(selectedItem))
                        {
                            FilesListView.SelectedItems.RemoveAt(i);
                        }
                    }
                }

                foreach (var item in targetItems)
                {
                    if (!currentlySelected.Contains(item))
                    {
                        FilesListView.SelectedItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                LogSuppressedError("UpdateSelectionByRect", ex);
            }
        }

        private static Rect GetBoundsRelativeTo(FrameworkElement element, UIElement relativeTo)
        {
            try
            {
                var topLeft = element.TranslatePoint(new System.Windows.Point(0, 0), relativeTo);
                return new Rect(topLeft, new System.Windows.Size(element.ActualWidth, element.ActualHeight));
            }
            catch (Exception ex)
            {
                LogSuppressedError("GetBoundsRelativeTo", ex);
                return Rect.Empty;
            }
        }

        private static Rect NormalizeRect(System.Windows.Point p1, System.Windows.Point p2)
        {
            var x = Math.Min(p1.X, p2.X);
            var y = Math.Min(p1.Y, p2.Y);
            var w = Math.Abs(p2.X - p1.X);
            var h = Math.Abs(p2.Y - p1.Y);
            return new Rect(x, y, w, h);
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T t)
                {
                    return t;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
