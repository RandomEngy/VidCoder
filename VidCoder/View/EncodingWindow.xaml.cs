using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using VidCoder.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using VidCoder.Properties;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for EncodingWindow.xaml
	/// </summary>
	public partial class EncodingWindow : Window
	{
		public EncodingWindow()
		{
			InitializeComponent();

			this.tabControl.SelectedIndex = Settings.Default.EncodingDialogLastTab;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			string placement = Settings.Default.EncodingDialogPlacement;
			if (string.IsNullOrEmpty(placement))
			{
				Rect workArea = SystemParameters.WorkArea;

				if (workArea.Width > Constants.TotalDefaultWidth && workArea.Height > Constants.TotalDefaultHeight)
				{
					double widthRemaining = workArea.Width - Constants.TotalDefaultWidth;
					double heightRemaining = workArea.Height - Constants.TotalDefaultHeight;

					this.Left = workArea.Left + widthRemaining / 2;
					this.Top = workArea.Top + heightRemaining / 2 + 452;
				}
			}
			else
			{
				this.SetPlacement(placement);
			}
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			Settings.Default.EncodingDialogPlacement = this.GetPlacement();
			Settings.Default.EncodingDialogLastTab = this.tabControl.SelectedIndex;
			Settings.Default.Save();
		}

		private void ToolBar_Loaded(object sender, RoutedEventArgs e)
		{
			UIUtilities.HideOverflowGrid(sender as ToolBar);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			this.DataContext = null;
		}
	}
}
