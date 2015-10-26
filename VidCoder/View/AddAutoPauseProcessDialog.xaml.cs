using System.Windows;
using System.Windows.Input;
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
			this.InitializeComponent();
		}

		private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var viewModel = this.DataContext as AddAutoPauseProcessDialogViewModel;
			viewModel.Accept.Execute(null);
		}
	}
}
