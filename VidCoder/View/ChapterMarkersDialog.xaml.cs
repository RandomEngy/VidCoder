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
using VidCoder.Properties;
using System.ComponentModel;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for ChapterMarkersDialog.xaml
	/// </summary>
	public partial class ChapterMarkersDialog : Window
	{
		public ChapterMarkersDialog()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			try
			{
				this.SetPlacement(Settings.Default.ChapterMarkersDialogPlacement);
			}
			catch { }
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			Settings.Default.ChapterMarkersDialogPlacement = this.GetPlacement();
			Settings.Default.Save();
		}

		private void TitleBoxGotFocus(object sender, RoutedEventArgs e)
		{
			TextBox titleBox = sender as TextBox;
			titleBox.SelectAll();
		}
	}
}
