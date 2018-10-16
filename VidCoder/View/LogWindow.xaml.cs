using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.AnyContainer;
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
		// The maximum number of Runs to add to the log window in a single dispatcher call
		private const int MaxRunsPerDispatch = 10;

		// The maximum number of lines to add to log window in a single dispatcher call
		private const int MaxLinesPerDispatch = 100;

		private readonly IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private readonly IAppThemeService appThemeService = StaticResolver.Resolve<IAppThemeService>();

		private readonly Queue<LogEntry> pendingEntries = new Queue<LogEntry>();
		private bool workerRunning;
		private readonly object pendingEntriesLock = new object();

		private readonly Dictionary<LogColor, Brush> logColorBrushMapping = new Dictionary<LogColor, Brush>();

		public LogWindow()
		{
			this.InitializeComponent();

			this.PopulateLogColorBrushMapping();
			this.AddExistingLogEntries();

			this.Loaded += (sender, e) =>
			{
				this.logTextBox.ScrollToEnd();
			};

			// Subscribe to events
			this.logger.EntryLogged += this.OnEntryLogged;
			this.logger.Cleared += this.OnCleared;

			this.appThemeService.AppThemeObservable.Skip(1).Subscribe(_ =>
			{
				this.Dispatcher.BeginInvoke(new Action(() =>
				{
					this.PopulateLogColorBrushMapping();
					this.logParagraph.Inlines.Clear();
					this.AddExistingLogEntries();
				}));
			});
		}

		private void PopulateLogColorBrushMapping()
		{
			this.logColorBrushMapping.Clear();
			this.logColorBrushMapping.Add(LogColor.Error, GetBrush("LogErrorBrush"));
			this.logColorBrushMapping.Add(LogColor.VidCoder, GetBrush("LogVidCoderBrush"));
			this.logColorBrushMapping.Add(LogColor.VidCoderWorker, GetBrush("LogVidCoderWorkerBrush"));
			this.logColorBrushMapping.Add(LogColor.Normal, GetBrush("WindowTextBrush"));
		}

		private static Brush GetBrush(string resourceName)
		{
			return (Brush)Application.Current.Resources[resourceName];
		}

		private void AddExistingLogEntries()
		{
			lock (this.logger.LogLock)
			{
				// Add all existing log entries
				ColoredLogGroup currentGroup = null;
				foreach (LogEntry entry in this.logger.LogEntries)
				{
					LogColor entryColor = GetEntryColor(entry);

					// If we need to start a new group
					if (currentGroup == null || entryColor != currentGroup.Color)
					{
						// Write out the last group (if it exists)
						if (currentGroup != null)
						{
							this.AddLogGroup(currentGroup);
						}

						// Start the new group
						currentGroup = new ColoredLogGroup(entryColor);
					}

					// Add the entry
					currentGroup.Entries.Add(entry);
				}

				// Add any items in the last group
				if (currentGroup != null)
				{
					this.AddLogGroup(currentGroup);
				}
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			this.logger.EntryLogged -= this.OnEntryLogged;
			this.logger.Cleared -= this.OnCleared;
		}

		private void OnEntryLogged(object sender, EventArgs<LogEntry> e)
		{
			lock (this.pendingEntriesLock)
			{
				this.pendingEntries.Enqueue(e.Value);
				if (!this.workerRunning)
				{
					this.workerRunning = true;
					this.Dispatcher.BeginInvoke(new Action(this.ProcessPendingEntries));
				}
			}
		}

		// Adds pending entries to the UI
		private void ProcessPendingEntries()
		{
			List<ColoredLogGroup> entryGroups;

			lock (this.pendingEntriesLock)
			{
				if (this.pendingEntries.Count == 0)
				{
					// If there are no items left, scroll to the end if we're already there, then bail
					if (this.logTextBox.VerticalOffset + this.logTextBox.ViewportHeight >= this.logTextBox.ExtentHeight)
					{
						this.Dispatcher.BeginInvoke(new Action(() =>
						{
							this.logTextBox.ScrollToEnd();
						}));
					}

					this.workerRunning = false;
					return;
				}

				// copy some pending items to groups
				entryGroups = new List<ColoredLogGroup>();

				ColoredLogGroup currentGroup = null;
				int currentLines = 0;
				while (this.pendingEntries.Count > 0 && entryGroups.Count < MaxRunsPerDispatch && currentLines < MaxLinesPerDispatch)
				{
					LogEntry entry = this.pendingEntries.Dequeue();
					LogColor entryColor = GetEntryColor(entry);
					if (currentGroup == null || entryColor != currentGroup.Color)
					{
						currentGroup = new ColoredLogGroup(entryColor);
						entryGroups.Add(currentGroup);
					}

					currentGroup.Entries.Add(entry);
					currentLines++;
				}
			}

			this.AddLogGroups(entryGroups);
			this.Dispatcher.BeginInvoke(new Action(this.ProcessPendingEntries));
		}

		private void OnCleared(object sender, EventArgs e)
		{
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
				this.logParagraph.Inlines.Clear();
			}));
		}

		private void AddLogGroups(IEnumerable<ColoredLogGroup> groups)
		{
			foreach (ColoredLogGroup group in groups)
			{
				this.AddLogGroup(group);
			}
		}

		private void AddLogGroup(ColoredLogGroup group)
		{
			Brush brush = this.logColorBrushMapping[group.Color];
			StringBuilder runText = new StringBuilder();

			foreach (LogEntry entry in group.Entries)
			{
				runText.AppendLine(entry.Text);
			}

			var run = new Run(runText.ToString());
			run.Foreground = brush;

			this.logParagraph.Inlines.Add(run);
		}

		private static LogColor GetEntryColor(LogEntry entry)
		{
			if (entry.LogType == LogType.Error)
			{
				return LogColor.Error;
				//return Colors.Red;
			}
			else if (entry.Source == LogSource.VidCoder)
			{
				return LogColor.VidCoder;
				//return Colors.DarkBlue;
			}
			else if (entry.Source == LogSource.VidCoderWorker)
			{
				return LogColor.VidCoderWorker;
				//return Colors.DarkGreen;
			}


			return LogColor.Normal;
			//return Colors.Black;
		}

		private class ColoredLogGroup
		{
			public ColoredLogGroup(LogColor color)
			{
				this.Entries = new List<LogEntry>();
				this.Color = color;
			}

			public IList<LogEntry> Entries { get; }

			public LogColor Color { get; }
		}

		private enum LogColor
		{
			Error,
			VidCoder,
			VidCoderWorker,
			Normal
		}
	}
}
