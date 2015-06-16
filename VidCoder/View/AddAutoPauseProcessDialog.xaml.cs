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
using VidCoder.Extensions;
using VidCoder.ViewModel;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for AddAutoPauseProcessDialog.xaml
	/// </summary>
	public partial class AddAutoPauseProcessDialog : Window
	{
		public AddAutoPauseProcessDialog()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetPlacementJson(Config.AddAutoPauseProcessDialogPlacement);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Config.AddAutoPauseProcessDialogPlacement = this.GetPlacementJson();
		}

		private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var viewModel = this.DataContext as AddAutoPauseProcessDialogViewModel;
			viewModel.AcceptCommand.Execute(null);
		}
	}
}
