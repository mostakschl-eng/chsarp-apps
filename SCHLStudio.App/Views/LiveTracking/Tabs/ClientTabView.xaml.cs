namespace SCHLStudio.App.Views.LiveTracking.Tabs
{
    public partial class ClientTabView : System.Windows.Controls.UserControl
    {
        public ClientTabView()
        {
            InitializeComponent();

            try
            {
                AddHandler(
                    System.Windows.UIElement.PreviewMouseWheelEvent,
                    new System.Windows.Input.MouseWheelEventHandler(OnPreviewMouseWheel),
                    handledEventsToo: true);
            }
            catch
            {
            }
        }

        private void OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                if (RootScrollViewer == null)
                {
                    return;
                }

                var current = RootScrollViewer.VerticalOffset;
                var next = current - e.Delta;
                if (next < 0) next = 0;
                if (next > RootScrollViewer.ScrollableHeight) next = RootScrollViewer.ScrollableHeight;

                RootScrollViewer.ScrollToVerticalOffset(next);
                e.Handled = true;
            }
            catch
            {
            }
        }

        private void ListView_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                if (RootScrollViewer == null)
                {
                    return;
                }

                var current = RootScrollViewer.VerticalOffset;
                var next = current - e.Delta;
                if (next < 0) next = 0;
                if (next > RootScrollViewer.ScrollableHeight) next = RootScrollViewer.ScrollableHeight;

                RootScrollViewer.ScrollToVerticalOffset(next);
                e.Handled = true;
            }
            catch
            {
            }
        }
    }
}
