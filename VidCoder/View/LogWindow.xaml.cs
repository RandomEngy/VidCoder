using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicData;
using ImTools;
using Microsoft.AnyContainer;
using ReactiveUI;
using SharpCompress.Common;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Services;
using WinCopies.Util;
using Math = System.Math;

namespace VidCoder.View;

/// <summary>
/// Interaction logic for LogWindow.xaml
/// </summary>
public partial class LogWindow : Window
{
	// The target chunk line count for log entries, may be higher
	private const int LogEntryTargetChunkLines = 60;

	private const double EstimatedLineHeight = 16.6666666;

	/// <summary>
	/// Buffer to load in, in device independent pixels
	/// </summary>
	private const double BufferDip = 1000;

	/// <summary>
	/// Buffer lines to load in on initial chunk load.
	/// </summary>
	private const int BufferLines = (int)(BufferDip / EstimatedLineHeight);

	private readonly IAppThemeService appThemeService = StaticResolver.Resolve<IAppThemeService>();
	private readonly LogCoordinator logCoordinator = StaticResolver.Resolve<LogCoordinator>();

	private readonly Queue<LoggedEntry> pendingEntries = new Queue<LoggedEntry>();
	private bool workerRunning;
	private readonly object pendingEntriesLock = new object();

	private IAppLogger logger;

	private readonly List<LogChunk> chunks = new List<LogChunk>();
	private int loadedChunkStart = 0;
	private int loadedChunkCount = 0;
	private double averageLineHeight = 0;
	private int lastFullyMeasuredChunk = -1;

	private bool suppressScrollListener = false;

	private readonly Dictionary<LogColor, Brush> logColorBrushMapping = new Dictionary<LogColor, Brush>();

	public LogWindow()
	{
		this.InitializeComponent();

		this.PopulateLogColorBrushMapping();
		this.listColumn.Width = new GridLength(Config.LogListPaneWidth);

		this.Loaded += (sender, e) =>
		{
			this.logTextBox.ScrollToEnd();
		};

		this.logCoordinator.Logs
			.Connect()
			.OnItemAdded(addedLogViewModel =>
			{
				DispatchUtilities.Invoke(() =>
				{
					this.logCoordinator.SelectedLog = addedLogViewModel;
					this.logsListBox.ScrollIntoView(addedLogViewModel);
				});
			})
			.Subscribe();

		this.logCoordinator.WhenAnyValue(x => x.SelectedLog).Subscribe(selectedLog =>
		{
			if (selectedLog != null)
			{
				this.DisconnectFromLogger();
				this.ConnectToLogger(selectedLog.Logger);
			}
		});

		this.appThemeService.AppThemeObservable.Skip(1).Subscribe(_ =>
		{
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
				this.PopulateLogColorBrushMapping();
				//this.ShowTextRuns();
			}));
		});
	}

	private void ConnectToLogger(IAppLogger newLogger)
	{
		this.logger = newLogger;
		lock (this.logger.LogLock)
		{
			this.logger.SuspendWriter();

			try
			{
				this.PopulateLogEntryChunks();
				this.ShowInitialTextRuns();
				newLogger.EntryLogged += this.OnEntryLogged;
			}
			finally
			{
				this.logger.ResumeWriter();
			}
		}

		this.logTextBox.ScrollToEnd();
	}

	private void DisconnectFromLogger()
	{
		if (this.logger != null)
		{
			this.logger.EntryLogged -= this.OnEntryLogged;
			this.logger = null;
		}
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

	/// <summary>
	/// Scan the file and divide into chunks that are indexed into the file for quick reads.
	/// </summary>
	private void PopulateLogEntryChunks()
	{
		this.chunks.Clear();

		using (var stream = new FileStream(this.logger.LogPath, FileMode.Open, FileAccess.Read))
		using (var reader = new TrackingStreamReader(stream, Encoding.UTF8))
		{
			string line;
			int currentChunkLineCount = 0;
			long currentChunkBytePosition = 0;
			long lastLineReadBytePosition = 0;

			while ((line = reader.ReadLine()) != null)
			{
				currentChunkLineCount++;

				// Check if we want to end this chunk
				if (currentChunkLineCount > LogEntryTargetChunkLines)
				{
					// If this is a marked line...
					LogColor color = LogEntryClassificationUtilities.GetLineColor(line);
					if (color != LogColor.NoChange)
					{
						// It's safe to make this the first line in the next chunk.
						this.chunks.Add(new LogChunk
						{
							LineCount = currentChunkLineCount - 1,
							BytePosition = currentChunkBytePosition,
							ByteCount = lastLineReadBytePosition - currentChunkBytePosition
						});
						currentChunkBytePosition = lastLineReadBytePosition;
						currentChunkLineCount = 1;
					}
				}

				// Getting ready to wrap up this chunk. Start noting the byte position before the next read.
				if (currentChunkLineCount >= LogEntryTargetChunkLines)
				{
					lastLineReadBytePosition = reader.BytePosition;
				}
			}

			if (currentChunkLineCount > 0)
			{
				this.chunks.Add(new LogChunk { LineCount = currentChunkLineCount, BytePosition = currentChunkBytePosition, ByteCount = reader.BytePosition - currentChunkBytePosition });
			}
		}
	}

	/// <summary>
	/// Initialize view state and show first text runs. You must have this.logger.LogLock and suspend writes before calling this.
	/// </summary>
	private void ShowInitialTextRuns()
	{
		this.logParagraph.Inlines.Clear();
		this.loadedChunkCount = 0;

		int lines = 0;

		int requiredLines = (int)(this.logScroller.ViewportHeight / EstimatedLineHeight) + BufferLines;

		// Count back from end of file to get the required number of lines
		for (int i = this.chunks.Count - 1; i >= 0; i--)
		{
			lines += this.chunks[i].LineCount;

			if (lines >= requiredLines || i == 0)
			{
				// Once we find our start chunk, go back to end of array
				for (int j = i; j < this.chunks.Count; j++)
				{
					this.LoadChunk(j, addToEnd: true);
					this.loadedChunkCount++;
				}

				this.loadedChunkStart = i;

				break;
			}
		}

		this.MeasureAndAdjustPlaceholders();

		this.logScroller.ScrollToEnd();
	}

	/// <summary>
	/// Measures all the loaded chunks.
	/// </summary>
	/// <returns>True if all the chunks were successfully loaded.</returns>
	private bool MeasureLoadedChunks()
	{
		bool allChunksLoaded = true;
		for (int i = this.loadedChunkStart; i < this.loadedChunkStart + this.loadedChunkCount; i++)
		{
			LogChunk chunk = this.chunks[i];
			if (chunk.MeasuredHeight == 0 && chunk.Runs != null)
			{
				Rect start = chunk.Runs[0].ElementStart.GetCharacterRect(LogicalDirection.Forward);
				Rect end = chunk.Runs[^1].ElementEnd.GetCharacterRect(LogicalDirection.Forward);

				if (start == Rect.Empty || end == Rect.Empty)
				{
					allChunksLoaded = false;
					System.Diagnostics.Debug.WriteLine("Could not measure chunk " + i);
				}
				else
				{
					chunk.MeasuredHeight = end.Top - start.Top;
				}
			}
		}

		return allChunksLoaded;
	}

	private double CalculateSpaceForChunkRange(int startIndex, int endIndexNonInclusive)
	{
		double space = 0;
		for (int i = startIndex; i < endIndexNonInclusive; i++)
		{
			LogChunk chunk = this.chunks[i];
			if (chunk.MeasuredHeight > 0)
			{
				space += chunk.MeasuredHeight;
			}
			else
			{
				space += chunk.LineCount * this.averageLineHeight;
			}
		}

		return space;
	}

	private void RefreshPlaceholderSpace()
	{
		double spaceAbove = this.CalculateSpaceForChunkRange(0, this.loadedChunkStart);
		double spaceBelow = this.CalculateSpaceForChunkRange(this.loadedChunkStart + this.loadedChunkCount, this.chunks.Count);

		this.logTextBox.Margin = new Thickness(0, spaceAbove, 0, spaceBelow);
	}

	private void MeasureAndAdjustPlaceholders()
	{
		this.suppressScrollListener = true;

		// Measure the newly loaded chunks
		if (this.MeasureLoadedChunks())
		{
			// If it worked, immediately recalculate placeholder size
			this.CalculateAverageLineHeightAndAdjustPlaceholders();
		}
		else
		{
			// If not, we need to run a dispatcher job and try again.
			this.Dispatcher.BeginInvoke(() =>
			{
				this.MeasureLoadedChunks();
				this.CalculateAverageLineHeightAndAdjustPlaceholders();
			});
		}
	}

	private void CalculateAverageLineHeightAndAdjustPlaceholders()
	{
		// Recalculate average line height.
		this.CalculateAverageLineHeight();

		// Now let's calculate the space that should be above + below the loaded chunks, based on the
		// known measured size and average line size for chunks that have not been loaded.
		this.RefreshPlaceholderSpace();

		this.Dispatcher.BeginInvoke(() =>
		{
			this.suppressScrollListener = false;
		});
	}

	/// <summary>
	/// Reads text from the given chunk index and displays it in the UI. You must have this.logger.LogLock and suspend writes before calling this.
	/// </summary>
	/// <param name="chunkIndex">The chunk index to read.</param>
	/// <param name="addToEnd">True if the text should be added to the end of the FlowDocument, false to add to the start.</param>
	private void LoadChunk(int chunkIndex, bool addToEnd)
	{
		this.suppressScrollListener = true;

		string[] lines = this.GetLines(chunkIndex, 1);

		LogChunk chunk = this.chunks[chunkIndex];
		chunk.Runs = new List<Run>();

		var sw2 = Stopwatch.StartNew();
		ColoredLogGroup currentGroup = null;
		foreach (string line in lines)
		{
			LogColor color = LogEntryClassificationUtilities.GetLineColor(line);

			// If we need to start a new group
			if (currentGroup == null || (color != LogColor.NoChange && color != currentGroup.Color))
			{
				// Write out the last group (if it exists)
				if (currentGroup != null)
				{
					chunk.Runs.Add(this.CreateLogGroupRun(currentGroup));
				}

				// Start the new group
				currentGroup = new ColoredLogGroup(color == LogColor.NoChange ? LogColor.Normal : color);
			}

			// Add the entry
			currentGroup.Entries.Add(line);
		}

		// Add any items in the last group
		if (currentGroup != null)
		{
			chunk.Runs.Add(this.CreateLogGroupRun(currentGroup));
		}

		if (addToEnd)
		{
			this.AddRunsToEnd(chunk.Runs);
		}
		else
		{
			this.AddRunsToStart(chunk.Runs);
		}

		chunk.MeasuredHeight = 0;

		this.suppressScrollListener = false;
	}

	private void CalculateAverageLineHeight()
	{
		int totalLines = 0;
		double totalHeight = 0;

		foreach (LogChunk chunk in this.chunks)
		{
			if (chunk.MeasuredHeight > 0 && chunk.LineCount > 0)
			{
				totalLines += chunk.LineCount;
				totalHeight += chunk.MeasuredHeight;
			}
		}

		if (totalLines > 0 && totalHeight > 0)
		{
			this.averageLineHeight = totalHeight / totalLines;
		}
		else
		{
			this.averageLineHeight = 0;
		}
	}

	/// <summary>
	/// Gets lines from the log file. You must have this.logger.LogLock and suspend writes before calling this.
	/// </summary>
	/// <param name="chunkStartIndex">The chunk to start reading.</param>
	/// <param name="chunkCount">The number of chunks to read.</param>
	/// <returns>The list of lines in the given chunks.</returns>
	private string[] GetLines(int chunkStartIndex, int chunkCount)
	{
		long totalBytes = 0;

		for (int i = 0; i < chunkCount; i++)
		{
			LogChunk chunk = this.chunks[chunkStartIndex + i];
			totalBytes += chunk.ByteCount;
		}

		using (var stream = new FileStream(this.logger.LogPath, FileMode.Open, FileAccess.Read))
		{
			byte[] buffer = new byte[totalBytes];

			stream.Position = this.chunks[chunkStartIndex].BytePosition;
			stream.Read(buffer, 0, (int)totalBytes);

			string logs = Encoding.UTF8.GetString(buffer);
			return logs.Split(
				Environment.NewLine,
				StringSplitOptions.RemoveEmptyEntries);
		}
	}

	private void UnloadChunk(int chunkIndex)
	{
		LogChunk chunk = this.chunks[chunkIndex];
		if (chunk.Runs == null)
		{
			// Chunk is not loaded
			return;
		}

		// Remove runs from document
		int firstRunIndex = this.logParagraph.Inlines.IndexOf(chunk.Runs[0]);
		if (firstRunIndex >= 0)
		{
			this.logParagraph.Inlines.RemoveRange(firstRunIndex, chunk.Runs.Count);
		}

		// Clear runs from storage
		chunk.Runs = null;
	}

	private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
	{
		this.logger.EntryLogged -= this.OnEntryLogged;
		Config.LogListPaneWidth = this.listColumn.ActualWidth;
	}

	private void OnEntryLogged(object sender, EventArgs<LoggedEntry> e)
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
		var entries = new List<LoggedEntry>();

		lock (this.pendingEntriesLock)
		{
			if (this.pendingEntries.Count == 0)
			{
				// If we are already at the bottom, scroll to the end, which will fire after the entries have been added to the UI.
				if (this.ScrolledToEnd()) 
				{
					this.logScroller.ScrollToEnd();
				}
				else
				{
					this.RefreshPlaceholderSpace();
				}

				this.workerRunning = false;
				return;
			}

			while (this.pendingEntries.Count > 0)
			{
				entries.Add(this.pendingEntries.Dequeue());
			}

			// Update chunk data
			int addedChunkCount = 0;
			foreach (LoggedEntry entry in entries)
			{
				LogChunk chunk;
				if (this.chunks.Count == 0 || this.chunks[^1].LineCount >= LogEntryTargetChunkLines)
				{
					chunk = new LogChunk
					{
						ByteCount = 0,
						LineCount = 0,
						BytePosition = this.chunks.Count == 0 ? 0 : this.chunks[^1].BytePosition + this.chunks[^1].ByteCount
					};
					this.chunks.Add(chunk);
					addedChunkCount++;
				}
				else
				{
					chunk = this.chunks[^1];
				}

				string[] lines = entry.Entry.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

				chunk.ByteCount += entry.ByteCount;
				chunk.LineCount += lines.Length;
				chunk.MeasuredHeight = 0;

				entry.Chunk = chunk;
			}

			// If we are scrolled to near the end, add the Runs to the document
			if (this.logScroller.VerticalOffset + this.logScroller.ViewportHeight + EstimatedLineHeight * BufferLines >= this.logScroller.ExtentHeight)
			{
				List<Run> runs = this.CreateRunsFromPendingEntries(entries);

				this.AddRunsToEnd(runs);

				this.loadedChunkCount += addedChunkCount;
			}
		}

		this.ProcessPendingEntries();
	}

	private List<Run> CreateRunsFromPendingEntries(IList<LoggedEntry> entries)
	{
		LogChunk currentChunk = null;
		ColoredLogGroup currentGroup = null;
		var runs = new List<Run>();

		foreach (LoggedEntry entry in entries)
		{
			LogColor entryColor = LogEntryClassificationUtilities.GetEntryColor(entry.Entry);
			if (currentGroup == null || (entryColor != LogColor.NoChange && entryColor != currentGroup.Color) || entry.Chunk != currentChunk)
			{
				if (currentGroup != null)
				{
					this.CommitColoredLogGroupToChunk(currentGroup, runs, entry.Chunk);
				}

				// Update the current group and chunk
				currentGroup = new ColoredLogGroup(entryColor == LogColor.NoChange ? LogColor.Normal : entryColor);
				currentChunk = entry.Chunk;
			}

			currentGroup.Entries.Add(entry.Entry.FormatMessage());
		}

		if (currentGroup != null)
		{
			this.CommitColoredLogGroupToChunk(currentGroup, runs, currentChunk);
		}

		return runs;
	}

	private void CommitColoredLogGroupToChunk(ColoredLogGroup currentGroup, List<Run> runs, LogChunk chunk)
	{
		// Create the Run
		Run run = this.CreateLogGroupRun(currentGroup);

		// Add the Run to the chunk registry so it can be removed properly when the chunk unloads
		if (chunk.Runs == null)
		{
			chunk.Runs = new List<Run>();
		}

		chunk.Runs.Add(run);

		// Add the Run to the result
		runs.Add(run);
	}

	/// <summary>
	/// Reads pending entries and puts them into log groups.
	/// </summary>
	/// <returns>The log groups.</returns>
	private List<ColoredLogGroup> GroupPendingEntries(IList<LoggedEntry> entries)
	{
		var entryGroups = new List<ColoredLogGroup>();

		ColoredLogGroup currentGroup = null;
		foreach (LoggedEntry entry in entries)
		{
			LogColor entryColor = LogEntryClassificationUtilities.GetEntryColor(entry.Entry);
			if (currentGroup == null || (entryColor != LogColor.NoChange && entryColor != currentGroup.Color))
			{
				currentGroup = new ColoredLogGroup(entryColor == LogColor.NoChange ? LogColor.Normal : entryColor);
				entryGroups.Add(currentGroup);
			}

			currentGroup.Entries.Add(entry.Entry.FormatMessage());
		}

		return entryGroups;
	}

	private bool ScrolledToEnd()
	{
		// Add a little buffer to account for rounding issues.
		return this.logScroller.VerticalOffset + this.logScroller.ViewportHeight + 1 >= this.logScroller.ExtentHeight;
	}

	private Run CreateLogGroupRun(ColoredLogGroup group)
	{
		StringBuilder runText = new StringBuilder();

		foreach (string entry in group.Entries)
		{
			runText.AppendLine(entry);
		}

		var run = new Run(runText.ToString());
		run.Foreground = this.logColorBrushMapping[group.Color];

		return run;
	}

	private void AddRunsToStart(IList<Run> runs)
	{
		for (int i = runs.Count - 1; i >= 0; i--)
		{
			Run run = runs[i];
			this.logParagraph.Inlines.InsertBefore(this.logParagraph.Inlines.FirstInline, run);
		}
	}

	private void AddRunsToEnd(IList<Run> runs)
	{
		foreach (Run run in runs)
		{
			this.logParagraph.Inlines.Add(run);
		}
	}

	private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (this.suppressScrollListener)
		{
			return;
		}

		// First, find out what chunks should be loaded based on the new viewport
		double viewport = this.logScroller.ViewportHeight;
		double offset = this.logScroller.VerticalOffset;
		double totalHeight = this.logScroller.ExtentHeight;

		// Find the first required chunk
		double topThreshold = offset - BufferDip;

		int newStartChunk = this.chunks.Count - 1;
		double height = 0;
		for (int i = 0; i < this.chunks.Count; i++)
		{
			LogChunk chunk = this.chunks[i];
			height += this.EstimateChunkHeight(chunk);

			if (height >= topThreshold)
			{
				newStartChunk = i;
				break;
			}
		}

		// Find the last required chunk
		double bottomThreshold = offset + viewport + BufferDip;

		int newEndChunk = newStartChunk;
		if (height < bottomThreshold)
		{
			for (int i = newStartChunk + 1; i < this.chunks.Count; i++)
			{
				LogChunk chunk = this.chunks[i];
				height += this.EstimateChunkHeight(chunk);

				newEndChunk = i;

				if (height > bottomThreshold)
				{
					// Adding this chunk's height has gone past the bottom threshold, we must be the last
					break;
				}
			}
		}

		int loadedChunkEnd = this.loadedChunkStart + this.loadedChunkCount - 1;

		if (newStartChunk == this.loadedChunkStart && newEndChunk == loadedChunkEnd)
		{
			// Desired chunk load range is unchanged
			return;
		}

		// Unload the old chunks
		// Chunks before the new range
		if (this.loadedChunkStart < newStartChunk)
		{
			for (int i = this.loadedChunkStart; i < newStartChunk; i++)
			{
				this.UnloadChunk(i);
			}
		}

		// Chunks after the new range
		if (loadedChunkEnd > newEndChunk)
		{
			for (int i = loadedChunkEnd; i > newEndChunk; i--)
			{
				this.UnloadChunk(i);
			}
		}

		if (newEndChunk < this.loadedChunkStart || newStartChunk > loadedChunkEnd)
		{
			// Disjoint. Load in forward order.
			this.LoadChunkRange(newStartChunk, newEndChunk - newStartChunk + 1, addToEnd: true);
		}
		else
		{
			if (newStartChunk < this.loadedChunkStart)
			{
				// Load new chunks at beginning.
				this.LoadChunkRange(newStartChunk, this.loadedChunkStart - newStartChunk, addToEnd: false);
			}

			if (newEndChunk >= this.loadedChunkStart + this.loadedChunkCount)
			{
				// Load new chunks at end.
				this.LoadChunkRange(loadedChunkEnd + 1, newEndChunk - loadedChunkEnd, addToEnd: true);
			}
		}

		// Update the loaded chunk markers
		this.loadedChunkStart = newStartChunk;
		this.loadedChunkCount = newEndChunk - newStartChunk + 1;

		this.MeasureAndAdjustPlaceholders();
	}

	private void LoadChunkRange(int startChunkIndex, int count, bool addToEnd)
	{
		lock (this.logger.LogLock)
		{
			this.logger.SuspendWriter();

			try
			{
				if (addToEnd)
				{
					for (int i = startChunkIndex; i < startChunkIndex + count; i++)
					{
						this.LoadChunk(i, addToEnd);
					}
				}
				else
				{
					for (int i = startChunkIndex + count - 1; i >= startChunkIndex; i--)
					{
						this.LoadChunk(i, addToEnd);
					}
				}
			}
			finally
			{
				this.logger.ResumeWriter();
			}
		}
	}

	private double EstimateChunkHeight(LogChunk chunk)
	{
		return chunk.MeasuredHeight == 0 ? chunk.LineCount * this.averageLineHeight : chunk.MeasuredHeight;
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

	private void ToolBar_Loaded(object sender, RoutedEventArgs e)
	{
		UIUtilities.HideOverflowGrid(sender as ToolBar);
	}
}
