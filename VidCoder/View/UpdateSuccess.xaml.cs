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
	/// This dialog is only needed to work around a problem where a modal dialog box is dismissed by the splash screen.
	/// </summary>
	public partial class UpdateSuccess : Window
	{
		public UpdateSuccess()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
	}
}
