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

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for MultipleTitlesDialog.xaml
	/// </summary>
	public partial class QueueTitlesDialog : Window
	{
		public QueueTitlesDialog()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetPlacement(Properties.Settings.Default.QueueTitlesDialogPlacement);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Properties.Settings.Default.QueueTitlesDialogPlacement = this.GetPlacement();
			Properties.Settings.Default.Save();
		}
	}
}
