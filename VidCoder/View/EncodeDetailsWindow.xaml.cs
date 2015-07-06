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
using VidCoder.Services;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for EncodeDetailsWindow.xaml
	/// </summary>
	public partial class EncodeDetailsWindow : Window
	{
		public EncodeDetailsWindow()
		{
			this.InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.PlaceDynamic(Config.EncodeDetailsWindowPlacement);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			Config.EncodeDetailsWindowPlacement = this.GetPlacementJson();
		}
	}
}
