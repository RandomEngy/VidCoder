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

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for LogWindow.xaml
	/// </summary>
	public partial class LogWindow : Window
	{
		public LogWindow()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetPlacement(Properties.Settings.Default.LogWindowPlacement);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Settings.Default.LogWindowPlacement = this.GetPlacement();
			Settings.Default.Save();
		}

		private void logTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.logTextBox.ScrollToEnd();
		}
	}
}
