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
using Microsoft.Practices.Unity;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.Services;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for LogWindow.xaml
	/// </summary>
	public partial class LogWindow : Window
	{
		private ILogger logger = Unity.Container.Resolve<ILogger>();

		public LogWindow()
		{
			InitializeComponent();

			lock (this.logger.LogLock)
			{
				// Add all existing log entries
				foreach (LogEntry entry in this.logger.LogEntries)
				{
					this.AddEntry(entry);
				}
			}

			this.Loaded += (sender, e) =>
			{
				this.logTextBox.ScrollToEnd();
			};

			// Subscribe to events
			this.logger.EntryLogged += this.OnEntryLogged;
			this.logger.Cleared += this.OnCleared;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.SetPlacement(Settings.Default.LogWindowPlacement);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			this.logger.EntryLogged -= this.OnEntryLogged;
			this.logger.Cleared -= this.OnCleared;

			Settings.Default.LogWindowPlacement = this.GetPlacement();
			Settings.Default.Save();
		}

		private void OnEntryLogged(object sender, EventArgs<LogEntry> e)
		{
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
				this.AddEntry(e.Value);
				this.logTextBox.ScrollToEnd();
			}));
		}

		private void OnCleared(object sender, EventArgs e)
		{
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
				this.logDocument.Blocks.Clear();
			}));
		}

		private void AddEntry(LogEntry entry)
		{
			var run = new Run(entry.Text);

			if (entry.LogType == LogType.Error)
			{
				run.Foreground = new SolidColorBrush(Colors.Red);
			}
			else if (entry.Source == LogSource.VidCoder)
			{
				run.Foreground = new SolidColorBrush(Colors.DarkBlue);
			}

			this.logDocument.Blocks.Add(new Paragraph(run));
		}
	}
}
