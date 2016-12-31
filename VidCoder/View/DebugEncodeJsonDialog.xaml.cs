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
	/// Interaction logic for DebugEncodeJsonDialog.xaml
	/// </summary>
	public partial class DebugEncodeJsonDialog : Window
	{
		public DebugEncodeJsonDialog()
		{
			this.InitializeComponent();
		}

		public string EncodeJson { get; set; }

		private void OnOKClick(object sender, RoutedEventArgs e)
		{
			this.EncodeJson = this.textBox.Text;
			this.Close();
		}
	}
}
