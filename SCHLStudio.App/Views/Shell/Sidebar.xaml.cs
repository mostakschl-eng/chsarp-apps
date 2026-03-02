using System.Windows;

using WpfUserControl = System.Windows.Controls.UserControl;

namespace SCHLStudio.App.Views.Shell
{
    public partial class Sidebar : WpfUserControl
    {
        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(Sidebar),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public Sidebar()
        {
            InitializeComponent();
        }
    }
}

