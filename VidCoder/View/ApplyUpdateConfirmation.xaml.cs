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
using System.Diagnostics;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder
{
	/// <summary>
	/// Interaction logic for ApplyUpdateConfirmation.xaml
	/// </summary>
	public partial class ApplyUpdateConfirmation : Window
	{
		private string changelogLinkString;

		public ApplyUpdateConfirmation()
		{
			InitializeComponent();

			this.changelogLinkString = Config.UpdateChangelogLocation;
		}

		public bool Accepted { get; private set; }

		private void yesButton_Click(object sender, RoutedEventArgs e)
		{
			this.Accepted = true;
			this.Close();
		}

		private void changelogLink_Click(object sender, RoutedEventArgs e)
		{
			FileService.Instance.LaunchUrl(this.changelogLinkString);
		}
	}
}
