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
	public partial class ChooseName : Window
	{
		public ChooseName()
		{
			this.InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.presetNameText.Focus();
			this.presetNameText.SelectAll();
		}
	}
}
