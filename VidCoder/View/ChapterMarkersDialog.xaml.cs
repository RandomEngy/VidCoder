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
using VidCoder.Extensions;
using System.Windows.Interop;

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

		private void TitleBoxGotFocus(object sender, RoutedEventArgs e)
		{
			TextBox titleBox = sender as TextBox;
			titleBox.SelectAll();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			this.DataContext = null;
		}
	}
}
