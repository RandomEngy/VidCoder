﻿using System;
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

		public ExceptionDialog(Exception exception)
		{
			this.exception = exception;

			InitializeComponent();

			this.subText.Text = MiscRes.ExceptionDialogSubText;

			this.errorIcon.Source = WpfSystemIcons.Error;
			this.exceptionTextBox.Text = exception + Environment.NewLine;
		}

		private void copyButton_Click(object sender, RoutedEventArgs e)
		{
			StaticResolver.Resolve<ClipboardService>().SetText(this.exception.ToString());
		}
	}
}
