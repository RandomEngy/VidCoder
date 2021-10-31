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
using System.Drawing;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.AnyContainer;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for ExceptionDialog.xaml
	/// </summary>
	public partial class ExceptionDialog : Window
	{
		private Exception exception;

		private string exceptionDetails;

		public ExceptionDialog(Exception exception)
		{
			this.exception = exception;

			InitializeComponent();

			this.subText.Text = MiscRes.ExceptionDialogSubText;

			this.errorIcon.Source = WpfSystemIcons.Error;

			string extraInfo = string.Empty;
			try
			{
				string portableText = Utilities.IsPortable ? "Portable" : "Installer";
				extraInfo = $"VidCoder {Utilities.VersionString} {portableText}";
			}
			catch { }

			this.exceptionDetails = extraInfo + Environment.NewLine + exception + Environment.NewLine; ;
			this.exceptionTextBox.Text = this.exceptionDetails;
		}

		private void copyButton_Click(object sender, RoutedEventArgs e)
		{
			StaticResolver.Resolve<ClipboardService>().SetText(this.exceptionDetails);
		}
	}
}
