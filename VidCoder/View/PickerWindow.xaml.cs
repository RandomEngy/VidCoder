using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VidCoder.ViewModel;

namespace VidCoder.View
{
    /// <summary>
    /// Interaction logic for PickerWindow.xaml
    /// </summary>
    public partial class PickerWindow : Window, IPickerWindowView
    {
	    private PickerWindowViewModel viewModel;

        public PickerWindow()
        {
            this.InitializeComponent();
			this.listColumn.Width = new GridLength(Config.PickerListPaneWidth);
	        this.DataContextChanged += (sender, args) =>
	        {
		        this.viewModel = args.NewValue as PickerWindowViewModel;
				this.viewModel.View = this;
	        };
        }

        private void PickerWindow_OnClosing(object sender, CancelEventArgs e)
        {
			Config.PickerListPaneWidth = this.listColumn.ActualWidth;
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            UIUtilities.HideOverflowGrid(sender as ToolBar);
        }

	    private void OnPresetComboPreviewKeyDown(object sender, KeyEventArgs e)
	    {
		    this.viewModel.HandlePresetComboKey(e);
	    }

		public void ScrollAudioSectionIntoView()
		{
			this.ScrollToElement(this.audioSectionTitle);
		}

		public void ScrollSubtitlesSectionIntoView()
		{
			this.ScrollToElement(this.subtitlesSectionTitle);
		}

		private void ScrollToElement(UIElement element)
		{
			var transform = element.TransformToVisual(this.mainScrollViewerContent);
			var positionInScrollViewer = transform.Transform(new Point(0, 0));

			this.mainScrollViewer.ScrollToVerticalOffset(positionInScrollViewer.Y);
		}
	}
}
