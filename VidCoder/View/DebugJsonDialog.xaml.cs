using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
	/// Interaction logic for DebugJsonDialog.xaml
	/// </summary>
	public partial class DebugJsonDialog : Window
	{
		public DebugJsonDialog(string title)
		{
			this.InitializeComponent();

			this.Title = title;
		}

		public string Json { get; set; }

		private void OnOKClick(object sender, RoutedEventArgs e)
		{
			this.Json = this.textBox.Text;
			this.Close();
		}
	}
}
