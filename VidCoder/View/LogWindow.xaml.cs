using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
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
				this.logger.SuspendWriter();

				try
				{
					// Add all existing log entries
					ColoredLogGroup currentGroup = null;
					using (var reader = new StreamReader(new FileStream(this.logger.LogPath, FileMode.Open, FileAccess.Read)))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							LogColor color = LogEntryClassificationUtilities.GetLineColor(line);
							bool isContinuation = LogEntryClassificationUtilities.LineIsContinuation(line);

							// If we need to start a new group
							if (currentGroup == null || color != currentGroup.Color && !isContinuation)
							{
								// Write out the last group (if it exists)
								if (currentGroup != null)
								{
									this.AddLogGroup(currentGroup);
								}

								// Start the new group
								currentGroup = new ColoredLogGroup(color);
							}

							// Add the entry
							currentGroup.Entries.Add(line);
						}
					}

					// Add any items in the last group
					if (currentGroup != null)
					{
						this.AddLogGroup(currentGroup);
					}
				}
				finally
				{
					this.logger.ResumeWriter();
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
					// If we are already at the bottom, scroll to the end, which will fire after the entries have been added to the UI.
					if (this.logTextBox.VerticalOffset + this.logTextBox.ViewportHeight >= this.logTextBox.ExtentHeight) 
					{
						this.logTextBox.ScrollToEnd();
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
					LogColor entryColor = LogEntryClassificationUtilities.GetEntryColor(entry);
					if (currentGroup == null || entryColor != currentGroup.Color)
					{
						currentGroup = new ColoredLogGroup(entryColor);
						entryGroups.Add(currentGroup);
					}

					currentGroup.Entries.Add(entry.Text);
					currentLines++;
				}
			}

			this.AddLogGroups(entryGroups);
			this.ProcessPendingEntries();
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

			foreach (string entry in group.Entries)
			{
				runText.AppendLine(entry);
			}

			var run = new Run(runText.ToString());
			run.Foreground = brush;

			this.logParagraph.Inlines.Add(run);
		}

		private class ColoredLogGroup
		{
			public ColoredLogGroup(LogColor color)
			{
				this.Entries = new List<string>();
				this.Color = color;
			}

			public IList<string> Entries { get; }

			public LogColor Color { get; }
		}
	}
}
