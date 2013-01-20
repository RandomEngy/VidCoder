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

namespace VidCoder.Controls
{
	using HandBrake.Interop.SourceData;

	/// <summary>
	/// Interaction logic for ChapterBar.xaml
	/// </summary>
	public partial class RangeBar : UserControl
	{
		private bool markersUpdated = false;
		private double totalSeconds;
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

		public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
			"Start",
			typeof (int),
			typeof (RangeBar),
			new PropertyMetadata(OnStartChanged));

		private static void OnStartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue != e.OldValue)
			{
				var chapterBar = d as RangeBar;
				chapterBar.UpdateSeekBarUI();
				chapterBar.UpdateMarkers();
			}
		}

		public int Start
		{
			get
			{
				return (int) GetValue(StartProperty);
			}

			set
			{
				SetValue(StartProperty, value);
			}
		}

		public static readonly DependencyProperty EndProperty = DependencyProperty.Register(
			"End",
			typeof (int),
			typeof (RangeBar),
			new PropertyMetadata(OnEndChanged));

		private static void OnEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue != e.OldValue)
			{
				var chapterBar = d as RangeBar;
				chapterBar.UpdateSeekBarUI();
				chapterBar.UpdateMarkers();
			}
		}

		public int End
		{
			get
			{
				return (int) GetValue(EndProperty);
			}

			set
			{
				SetValue(EndProperty, value);
			}
		}

		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
			"Title",
			typeof (Title),
			typeof (RangeBar),
			new PropertyMetadata(OnTitleChanged));

		public Title Title
		{
			get
			{
				return (Title) GetValue(TitleProperty);
			}

			set
			{
				SetValue(TitleProperty, value);
			}
		}

		private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var title = (Title) e.NewValue;
			var chapterBar = d as RangeBar;

			if (title == null)
			{
				return;
			}

			List<Chapter> chapters = title.Chapters;

			chapterBar.markersUpdated = false;

			chapterBar.totalSeconds = title.Duration.TotalSeconds;

			double seconds = 0;
			chapterBar.chapterFractions = new List<double>();
			foreach (Chapter chapter in chapters)
			{
				seconds += chapter.Duration.TotalSeconds;
				chapterBar.chapterFractions.Add(Math.Min(seconds / chapterBar.totalSeconds, 1));
			}

			chapterBar.UpdateSeekBarUI();
			chapterBar.UpdateMarkers();
		}

		//public static readonly DependencyProperty ChaptersProperty = DependencyProperty.Register(
		//	"Chapters",
		//	typeof (List<Chapter>),
		//	typeof (ChapterBar),
		//	new PropertyMetadata(OnChaptersChanged));

		//private static void OnChaptersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		//{
		//	var chapters = (List<Chapter>) e.NewValue;
		//	var chapterBar = d as ChapterBar;

		//	if (chapters == null)
		//	{
		//		return;
		//	}

		//	chapterBar.markersUpdated = false;

		//	chapterBar.totalSeconds = 0;
		//	foreach (Chapter chapter in chapters)
		//	{
		//		chapterBar.totalSeconds += chapter.Duration.TotalSeconds;
		//	}

		//	double seconds = 0;
		//	chapterBar.chapterFractions = new List<double>();
		//	foreach (Chapter chapter in chapters)
		//	{
		//		seconds += chapter.Duration.TotalSeconds;
		//		chapterBar.chapterFractions.Add(Math.Min(seconds / chapterBar.totalSeconds, 1));
		//	}

		//	chapterBar.UpdateSeekBarUI();
		//	chapterBar.UpdateMarkers();
		//}

		//public List<Chapter> Chapters
		//{
		//	get
		//	{
		//		return (List<Chapter>) GetValue(ChaptersProperty);
		//	}

		//	set
		//	{
		//		SetValue(ChaptersProperty, value);
		//	}
		//}

		public List<Chapter> Chapters
		{
			get
			{
				if (this.Title == null)
				{
					return null;
				}

				return this.Title.Chapters;
			}
		} 

		public static readonly DependencyProperty StartSecondsProperty = DependencyProperty.Register(
			"StartSeconds",
			typeof (double),
			typeof (RangeBar),
			new PropertyMetadata(OnStartSecondsChanged));

		public double StartSeconds
		{
			get
			{
				return (double) GetValue(StartSecondsProperty);
			}

			set
			{
				SetValue(StartSecondsProperty, value);
			}
		}

		private static void OnStartSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue != e.OldValue)
			{
				var chapterBar = d as RangeBar;
				chapterBar.UpdateSeekBarUI();
				chapterBar.UpdateMarkers();
			}
		}

		public static readonly DependencyProperty EndSecondsProperty = DependencyProperty.Register(
			"EndSeconds",
			typeof (double),
			typeof (RangeBar),
			new PropertyMetadata(OnEndSecondsChanged));

		public double EndSeconds
		{
			get
			{
				return (double) GetValue(EndSecondsProperty);
			}

			set
			{
				SetValue(EndSecondsProperty, value);
			}
		}

		private static void OnEndSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
				if (this.Start <= 0 || this.End <= 0 || this.Chapters == null || this.Start > this.End || this.Start > this.Chapters.Count || this.End > this.Chapters.Count)
				{
					return;
				}
			}
			else
			{
				if (this.StartSeconds < 0 || this.StartSeconds > this.totalSeconds || this.EndSeconds <= 0 || this.EndSeconds > this.totalSeconds || this.StartSeconds > this.EndSeconds)
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
				startBarFraction = this.GetBarFractionFromChapter(this.Start - 1);
				endBarFraction = this.GetBarFractionFromChapter(this.End);
			}
			else
			{
				startBarFraction = this.GetBarFractionFromSeconds(this.StartSeconds);
				endBarFraction = this.GetBarFractionFromSeconds(this.EndSeconds);
			}

			this.preColumn.Width = new GridLength(startBarFraction, GridUnitType.Star);
			this.rangeColumn.Width = new GridLength(endBarFraction - startBarFraction, GridUnitType.Star);
			this.postColumn.Width = new GridLength(1 - endBarFraction, GridUnitType.Star);

			//double leftRadius = this.Start == 1 ? 5 : 0;
			//double rightRadius = this.End == this.Chapters.Count ? 5 : 0;

			//this.seekBarFilledBorder.CornerRadius = new CornerRadius(leftRadius, rightRadius, rightRadius, leftRadius);
		}

		private void UpdateMarkers()
		{
			if (this.markersUpdated)
			{
				return;
			}

			if (this.ChaptersEnabled)
			{
				if (this.Start <= 0 || this.End <= 0 || this.Chapters == null)
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
				double seconds = 0;
				for (int i = 1; i < this.Chapters.Count - 1; i++)
				{
					seconds += this.Chapters[i - 1].Duration.TotalSeconds;

					var marker = new Polygon
						{
							Style = this.FindResource("SeekBarTick") as Style,
							Margin = new Thickness(barWidth * (seconds / this.totalSeconds) - 1, 0, 0, 0),
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
			double seconds = 0;
			for (int i = 1; i <= chapter; i++)
			{
				seconds += this.Chapters[i - 1].Duration.TotalSeconds;
			}

			return GetBarFractionFromSeconds(seconds);
		}

		private double GetBarFractionFromSeconds(double seconds)
		{
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

			Point location = e.GetPosition(this);

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
					this.Start = this.Chapters.Count;
				}
				else
				{
					this.Start = slot + 1;
				}

				if (this.Start > this.End)
				{
					this.End = this.Start;
				}
			}
			else
			{
				if (slot == 0)
				{
					this.End = 1;
				}
				else
				{
					this.End = slot;
				}

				if (this.Start > this.End)
				{
					this.Start = this.End;
				}
			}
		}

		// Gets the slot the mouse is pointing to (starts at 0 and goes to chapters.Count)
		private int GetSlot(Point location)
		{
			double width = this.ActualWidth;
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
			double width = this.ActualWidth;
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

			double secondsStart, secondsEnd;
			if (start)
			{
				secondsStart = Math.Round(rawSeconds);
				secondsEnd = this.EndSeconds;
				if (secondsStart > this.totalSeconds - Constants.SecondsRangeBuffer)
				{
					secondsStart = this.totalSeconds - Constants.SecondsRangeBuffer;
				}

				if (secondsEnd < secondsStart + Constants.SecondsRangeBuffer)
				{
					secondsEnd = secondsStart + Constants.SecondsRangeBuffer;
				}

				if (secondsEnd > this.totalSeconds)
				{
					secondsEnd = totalSeconds;
				}
			}
			else
			{
				secondsStart = this.StartSeconds;
				secondsEnd = fraction == 1 ? rawSeconds : Math.Round(rawSeconds);

				if (secondsEnd < Constants.SecondsRangeBuffer)
				{
					secondsEnd = Constants.SecondsRangeBuffer;
				}

				if (secondsStart > secondsEnd - Constants.SecondsRangeBuffer)
				{
					secondsStart = secondsEnd - Constants.SecondsRangeBuffer;
				}

				if (secondsStart < 0)
				{
					secondsStart = 0;
				}
			}

			this.StartSeconds = secondsStart;
			this.EndSeconds = secondsEnd;
		}
	}
}
