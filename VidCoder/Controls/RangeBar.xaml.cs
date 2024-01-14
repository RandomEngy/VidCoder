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
using System.Windows.Navigation;
using System.Windows.Shapes;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Extensions;
using VidCoderCommon.Extensions;

namespace VidCoder.Controls;

/// <summary>
/// Interaction logic for ChapterBar.xaml
/// </summary>
public partial class RangeBar : UserControl
{
	private bool markersUpdated = false;
	private double totalSeconds;
	private TimeSpan totalDuration;
	private List<double> chapterFractions;

	private bool captured;
	private bool startCaptured;

	public RangeBar()
	{
		InitializeComponent();

		this.UpdateSeekBarUI();
	}

	public static readonly DependencyProperty ChaptersEnabledProperty = DependencyProperty.Register(
		"ChaptersEnabled",
		typeof (bool),
		typeof (RangeBar),
		new PropertyMetadata(OnChaptersEnabledChanged));

	public bool ChaptersEnabled
	{
		get
		{
			return (bool) GetValue(ChaptersEnabledProperty);
		}

		set
		{
			SetValue(ChaptersEnabledProperty, value);
		}
	}

	private static void OnChaptersEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != e.OldValue)
		{
			var chapterBar = d as RangeBar;

			chapterBar.markersUpdated = false;

			chapterBar.UpdateSeekBarUI();
			chapterBar.UpdateMarkers();
		}
	}

	public static readonly DependencyProperty StartChapterProperty = DependencyProperty.Register(
		"StartChapter",
		typeof (int),
		typeof (RangeBar),
		new PropertyMetadata(OnStartChapterChanged));

	public int StartChapter
	{
		get
		{
			return (int)GetValue(StartChapterProperty);
		}

		set
		{
			SetValue(StartChapterProperty, value);
		}
	}

	private static void OnStartChapterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != e.OldValue)
		{
			var chapterBar = d as RangeBar;
			chapterBar.UpdateSeekBarUI();
			chapterBar.UpdateMarkers();
		}
	}

	public static readonly DependencyProperty EndChapterProperty = DependencyProperty.Register(
		"EndChapter",
		typeof (int),
		typeof (RangeBar),
		new PropertyMetadata(OnEndChapterChanged));

	public int EndChapter
	{
		get
		{
			return (int)GetValue(EndChapterProperty);
		}

		set
		{
			SetValue(EndChapterProperty, value);
		}
	}

	private static void OnEndChapterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != e.OldValue)
		{
			var chapterBar = d as RangeBar;
			chapterBar.UpdateSeekBarUI();
			chapterBar.UpdateMarkers();
		}
	}

	public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
		"Title",
		typeof(SourceTitle),
		typeof (RangeBar),
		new PropertyMetadata(OnTitleChanged));

	public SourceTitle Title
	{
		get
		{
			return (SourceTitle)GetValue(TitleProperty);
		}

		set
		{
			SetValue(TitleProperty, value);
		}
	}

	private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var title = (SourceTitle)e.NewValue;
		var chapterBar = d as RangeBar;

		if (title == null)
		{
			return;
		}

		List<SourceChapter> chapters = title.ChapterList;

		chapterBar.markersUpdated = false;

		chapterBar.totalDuration = title.Duration.ToSpan();
		chapterBar.totalSeconds = HandBrakeUnitConversionHelpers.PtsToSeconds((ulong)title.Duration.Ticks);

		ulong pts = 0;
		chapterBar.chapterFractions = new List<double>();
		foreach (SourceChapter chapter in chapters)
		{
			pts += (ulong)chapter.Duration.Ticks;
			chapterBar.chapterFractions.Add(Math.Min(HandBrakeUnitConversionHelpers.PtsToSeconds(pts) / chapterBar.totalSeconds, 1));
		}

		chapterBar.UpdateSeekBarUI();
		chapterBar.UpdateMarkers();
	}

	public List<SourceChapter> Chapters
	{
		get
		{
			if (this.Title == null)
			{
				return null;
			}

			return this.Title.ChapterList;
		}
	}

	public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register(
		"StartTime",
		typeof (TimeSpan),
		typeof (RangeBar),
		new PropertyMetadata(OnStartTimeChanged));

	public TimeSpan StartTime
	{
		get
		{
			return (TimeSpan) GetValue(StartTimeProperty);
		}

		set
		{
			SetValue(StartTimeProperty, value);
		}
	}

	private static void OnStartTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != e.OldValue)
		{
			var chapterBar = d as RangeBar;
			chapterBar.UpdateSeekBarUI();
			chapterBar.UpdateMarkers();
		}
	}

	public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register(
		"EndTime",
		typeof (TimeSpan),
		typeof (RangeBar),
		new PropertyMetadata(OnEndTimeChanged));

	public TimeSpan EndTime
	{
		get
		{
			return (TimeSpan) GetValue(EndTimeProperty);
		}

		set
		{
			SetValue(EndTimeProperty, value);
		}
	}

	private static void OnEndTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != e.OldValue)
		{
			var chapterBar = d as RangeBar;
			chapterBar.UpdateSeekBarUI();
			chapterBar.UpdateMarkers();
		}
	}

	private void UpdateSeekBarUI()
	{
		if (this.ChaptersEnabled)
		{
			if (this.StartChapter <= 0 || 
				this.EndChapter <= 0 ||
				this.Chapters == null || 
				this.StartChapter > this.EndChapter ||
				this.StartChapter > this.Chapters.Count || 
				this.EndChapter > this.Chapters.Count)
			{
				return;
			}
		}
		else
		{
			if (this.StartTime < TimeSpan.Zero || 
				this.StartTime > this.totalDuration || 
				this.EndTime <= TimeSpan.Zero ||
				this.EndTime > this.totalDuration || 
				this.StartTime > this.EndTime)
			{
				return;
			}
		}

		double barWidth = this.barHolder.ActualWidth;

		if (barWidth == 0)
		{
			return;
		}

		double startBarFraction, endBarFraction;
		if (this.ChaptersEnabled)
		{
			startBarFraction = this.GetBarFractionFromChapter(this.StartChapter - 1);
			endBarFraction = this.GetBarFractionFromChapter(this.EndChapter);
		}
		else
		{
			startBarFraction = this.GetBarFractionFromTime(this.StartTime);
			endBarFraction = this.GetBarFractionFromTime(this.EndTime);
		}

		this.preColumn.Width = new GridLength(startBarFraction, GridUnitType.Star);
		this.rangeColumn.Width = new GridLength(endBarFraction - startBarFraction, GridUnitType.Star);
		this.postColumn.Width = new GridLength(1 - endBarFraction, GridUnitType.Star);
	}

	private void UpdateMarkers()
	{
		if (this.markersUpdated)
		{
			return;
		}

		if (this.ChaptersEnabled)
		{
			if (this.StartChapter <= 0 || this.EndChapter <= 0 || this.Chapters == null)
			{
				return;
			}
		}

		double barWidth = this.barHolder.ActualWidth;

		if (barWidth == 0)
		{
			return;
		}

		this.markersGrid.Children.Clear();

		if (this.ChaptersEnabled)
		{
			ulong pts = 0;
			for (int i = 1; i < this.Chapters.Count - 1; i++)
			{
				pts += (ulong)this.Chapters[i - 1].Duration.Ticks;

				var marker = new Polygon
					{
						Style = this.FindResource("SeekBarTick") as Style,
						Margin = new Thickness(barWidth * (HandBrakeUnitConversionHelpers.PtsToSeconds(pts) / this.totalSeconds) - 1, 0, 0, 0),
						HorizontalAlignment = HorizontalAlignment.Left,
						VerticalAlignment = VerticalAlignment.Top
					};

				this.markersGrid.Children.Add(marker);
			}
		}

		for (int minutes = 10; minutes * 60 < this.totalSeconds; minutes += 10)
		{
			string style = minutes % 60 == 0 ? "SeekBarBigTick" : "SeekBarTick";

			var marker = new Polygon
			{
				Style = this.FindResource(style) as Style,
				Margin = new Thickness(barWidth * ((minutes * 60) / this.totalSeconds) - 1, 0, 0, 0),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Bottom
			};

			this.markersGrid.Children.Add(marker);
		}

		this.markersUpdated = true;
	}

	private double GetBarFractionFromChapter(int chapter)
	{
		ulong pts = 0;
		for (int i = 1; i <= chapter; i++)
		{
			pts += (ulong)this.Chapters[i - 1].Duration.Ticks;
		}

		return GetBarFractionFromSeconds(HandBrakeUnitConversionHelpers.PtsToSeconds(pts));
	}

	private double GetBarFractionFromTime(TimeSpan time)
	{
		return GetBarFractionFromSeconds(time.TotalSeconds);
	}

	private double GetBarFractionFromSeconds(double seconds)
	{
		if (this.totalSeconds <= 0)
		{
			return 0;
		}

		double fraction = seconds / this.totalSeconds;
		if (fraction > 1)
		{
			fraction = 1;
		}

		if (fraction < 0)
		{
			fraction = 0;
		}

		return fraction;
	}

	private void BarHolder_OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		this.markersUpdated = false;

		this.UpdateSeekBarUI();
		this.UpdateMarkers();
	}

	private void ChapterBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		this.captured = true;
		this.startCaptured = true;
		this.HandleMouseEvent(e, start: true);
		this.CaptureMouse();
		this.Focus();
	}

	private void ChapterBar_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		this.captured = false;
		this.ReleaseMouseCapture();
	}

	private void ChapterBar_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		this.captured = true;
		this.startCaptured = false;
		this.HandleMouseEvent(e, start: false);
		this.CaptureMouse();
		this.Focus();
	}

	private void ChapterBar_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
	{
		this.captured = false;
		this.ReleaseMouseCapture();
	}

	private void ChapterBar_OnMouseMove(object sender, MouseEventArgs e)
	{
		if (this.captured)
		{
			this.HandleMouseEvent(e, this.startCaptured);
		}
	}

	private void HandleMouseEvent(MouseEventArgs e, bool start)
	{
		if (!this.IsEnabled)
		{
			return;
		}

		Point location = e.GetPosition(this.barHolder);

		if (this.ChaptersEnabled)
		{
			this.HandleMouseEventChapters(start, location);
		}
		else
		{
			this.HandleMouseEventNoChapters(start, location);
		}
	}

	private void HandleMouseEventChapters(bool start, Point location)
	{
		int slot = this.GetSlot(location);
		if (start)
		{
			if (slot == this.Chapters.Count)
			{
				this.StartChapter = this.Chapters.Count;
			}
			else
			{
				this.StartChapter = slot + 1;
			}

			if (this.StartChapter > this.EndChapter)
			{
				this.EndChapter = this.StartChapter;
			}
		}
		else
		{
			if (slot == 0)
			{
				this.EndChapter = 1;
			}
			else
			{
				this.EndChapter = slot;
			}

			if (this.StartChapter > this.EndChapter)
			{
				this.StartChapter = this.EndChapter;
			}
		}
	}

	// Gets the slot the mouse is pointing to (starts at 0 and goes to chapters.Count)
	private int GetSlot(Point location)
	{
		double width = this.barHolder.ActualWidth;
		double fraction = location.X / width;

		if (fraction < Math.Abs(this.chapterFractions[0] - fraction))
		{
			return 0;
		}

		if (fraction >= this.chapterFractions[this.chapterFractions.Count - 1])
		{
			return this.chapterFractions.Count;
		}

		// Find the slot with the closest fraction
		int low = 0;
		int high = this.Chapters.Count - 1;

		while (high - low > 1)
		{
			int mid = (low + high) / 2;
			if (fraction <= this.chapterFractions[mid])
			{
				high = mid;
			}
			else
			{
				low = mid;
			}
		}

		// Add 1 when returning because the array search was checking 0-based chapters and we need to return the slot
		if (Math.Abs(this.chapterFractions[low] - fraction) < Math.Abs(this.chapterFractions[high] - fraction))
		{
			return low + 1;
		}

		return high + 1;
	}

	private void HandleMouseEventNoChapters(bool start, Point location)
	{
		double width = this.barHolder.ActualWidth;
		double fraction = location.X / width;
		if (fraction < 0)
		{
			fraction = 0;
		}

		if (fraction > 1)
		{
			fraction = 1;
		}

		double rawSeconds = this.totalSeconds * fraction;

		TimeSpan startTime, endTime;
		if (start)
		{
			startTime = TimeSpan.FromSeconds(Math.Round(rawSeconds));
			endTime = this.EndTime;

			if (startTime > this.totalDuration - Constants.TimeRangeBuffer)
			{
				startTime = this.totalDuration - Constants.TimeRangeBuffer;
			}

			if (endTime < startTime + Constants.TimeRangeBuffer)
			{
				endTime = startTime + Constants.TimeRangeBuffer;
			}

			TimeSpan maxDuration = TimeSpan.FromSeconds(Math.Floor(this.totalDuration.TotalSeconds));

			if (endTime > maxDuration)
			{
				endTime = maxDuration;
			}
		}
		else
		{
			startTime = this.StartTime;
			endTime = fraction == 1 ? TimeSpan.FromSeconds(Math.Floor(this.totalDuration.TotalSeconds)) : TimeSpan.FromSeconds(Math.Round(rawSeconds));

			if (endTime < Constants.TimeRangeBuffer)
			{
				endTime = Constants.TimeRangeBuffer;
			}

			if (startTime > endTime - Constants.TimeRangeBuffer)
			{
				startTime = endTime - Constants.TimeRangeBuffer;
			}

			if (startTime < TimeSpan.Zero)
			{
				startTime = TimeSpan.Zero;
			}
		}

		this.StartTime = startTime;
		this.EndTime = endTime;
	}
}
