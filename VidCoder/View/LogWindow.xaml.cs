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
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for LogWindow.xaml
	/// </summary>
	public partial class LogWindow : Window
	{
		private ILogger logger = Ioc.Container.GetInstance<ILogger>();

		private int pendingLines;
		private object writeLock = new object();

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

            this.RegisterGlobalHotkeys();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.PlaceDynamic(Config.LogWindowPlacement);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			this.logger.EntryLogged -= this.OnEntryLogged;
			this.logger.Cleared -= this.OnCleared;

			Config.LogWindowPlacement = this.GetPlacementXml();
		}

		private void OnEntryLogged(object sender, EventArgs<LogEntry> e)
		{
			lock (this.writeLock)
			{
				this.pendingLines++;

				this.Dispatcher.BeginInvoke(new Action(() =>
					{
						this.AddEntry(e.Value);

						lock (this.writeLock)
						{
							this.pendingLines--;

							if (this.pendingLines == 0)
							{
								// Scrolling to the end can be a slow operation so only do it when we've run out of lines to write.
								this.logTextBox.ScrollToEnd();
							}
						}
					}),
					System.Windows.Threading.DispatcherPriority.Background);
			}
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
			else if (entry.Source == LogSource.VidCoderWorker)
			{
				run.Foreground = new SolidColorBrush(Colors.DarkGreen);
			}

			this.logDocument.Blocks.Add(new Paragraph(run));
		}
	}
}
