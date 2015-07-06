using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VidCoder.View
{
    /// <summary>
    /// Interaction logic for PickerWindow.xaml
    /// </summary>
    public partial class PickerWindow : Window
    {
        public PickerWindow()
        {
            this.InitializeComponent();
			this.listColumn.Width = new GridLength(Config.PickerListPaneWidth);
        }

        private void PickerWindow_OnClosing(object sender, CancelEventArgs e)
        {
			Config.PickerListPaneWidth = this.listColumn.ActualWidth;
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            UIUtilities.HideOverflowGrid(sender as ToolBar);
        }
    }
}
