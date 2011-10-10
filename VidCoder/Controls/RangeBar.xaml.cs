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
	/// <summary>
	/// Interaction logic for RangeBar.xaml
	/// </summary>
	public partial class RangeBar : UserControl
	{
		public RangeBar()
		{
			InitializeComponent();

			this.RefreshRange();
		}

		public const string StartTimePropertyName = "StartTime";
		public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register(
			StartTimePropertyName,
			typeof(TimeSpan),
			typeof(RangeBar),
			new UIPropertyMetadata(OnTimeChanged));
		public TimeSpan StartTime
		{
			get
			{
				return (TimeSpan)GetValue(StartTimeProperty);
			}
			set
			{
				SetValue(StartTimeProperty, value);
			}
		}

		public const string EndTimePropertyName = "EndTime";
		public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register(
			EndTimePropertyName,
			typeof(TimeSpan),
			typeof(RangeBar),
			new UIPropertyMetadata(OnTimeChanged));
		public TimeSpan EndTime
		{
			get
			{
				return (TimeSpan)GetValue(EndTimeProperty);
			}
			set
			{
				SetValue(EndTimeProperty, value);
			}
		}

		public const string DurationPropertyName = "Duration";
		public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
			DurationPropertyName,
			typeof(TimeSpan),
			typeof(RangeBar),
			new UIPropertyMetadata(OnTimeChanged));
		public TimeSpan Duration
		{
			get
			{
				return (TimeSpan)GetValue(DurationProperty);
			}
			set
			{
				SetValue(DurationProperty, value);
			}
		}

		private static void OnTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			if (eventArgs.NewValue != eventArgs.OldValue)
			{
				var rangeBar = dependencyObject as RangeBar;

				rangeBar.RefreshRange();
			}
		}

		private void RefreshRange()
		{
			if (this.Duration == TimeSpan.Zero ||
				this.StartTime > this.EndTime)
			{
				SetRange(1, 0, 0);
				return;
			}

			double beforeSeconds = this.StartTime.TotalSeconds;
			double rangeSeconds = (this.EndTime - this.StartTime).TotalSeconds;
			double afterSeconds = (this.Duration - this.EndTime).TotalSeconds;

			if (afterSeconds < 0)
			{
				afterSeconds = 0;
			}

			SetRange(beforeSeconds, rangeSeconds, afterSeconds);
		}

		private void SetRange(double before, double range, double after)
		{
			this.beforeColumn.Width = new GridLength(before, GridUnitType.Star);
			this.filledColumn.Width = new GridLength(range, GridUnitType.Star);
			this.afterColumn.Width = new GridLength(after, GridUnitType.Star);
		}
	}
}
