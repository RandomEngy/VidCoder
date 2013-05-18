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
	/// Interaction logic for TimeBox.xaml
	/// </summary>
	public partial class TimeBox : UserControl
	{
		private bool suppressRefresh;

		public TimeBox()
		{
			InitializeComponent();

			this.RefreshTimeBox();
		}

		public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
			"Time",
			typeof (TimeSpan),
			typeof (TimeBox),
			new PropertyMetadata(OnTimeChanged));

		public TimeSpan Time
		{
			get
			{
				return (TimeSpan) GetValue(TimeProperty);
			}

			set
			{
				SetValue(TimeProperty, value);
			}
		}

		private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue != e.OldValue)
			{
				var timeBox = d as TimeBox;

				if (!timeBox.suppressRefresh)
				{
					timeBox.RefreshTimeBox();
				}
			}
		}

		public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
			"Maximum",
			typeof (TimeSpan),
			typeof (TimeBox),
			new UIPropertyMetadata(TimeSpan.MaxValue));

		public TimeSpan Maximum
		{
			get
			{
				return (TimeSpan) GetValue(MaximumProperty);
			}

			set
			{
				SetValue(MaximumProperty, value);
			}
		}

		private void RefreshTimeBox()
		{
			this.timeBox.Text = this.Time.ToString("g");
		}

		private void TimeBoxGotFocus(object sender, RoutedEventArgs e)
		{
		}

		private void TimeBoxLostFocus(object sender, RoutedEventArgs e)
		{
			this.UpdateTimeBindingFromBox();
			this.RefreshTimeBox();
		}

		private void TimeBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			foreach (char c in e.Text)
			{
				if (!char.IsNumber(c) && c != ':' && c != '.')
				{
					e.Handled = true;
					return;
				}
			}
		}

		private void TimeBoxPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				e.Handled = true;
			}
		}

		private void TimeBoxTextChanged(object sender, TextChangedEventArgs e)
		{
			this.UpdateTimeBindingFromBox();
		}

		private void UpdateTimeBindingFromBox()
		{
			TimeSpan newTime;
			if (TimeSpan.TryParse(this.timeBox.Text, out newTime))
			{
				if (this.TimeIsValid(newTime))
				{
					if (newTime != this.Time)
					{
						// While updating the binding we don't need to react to the change.
						this.suppressRefresh = true;
						this.Time = newTime;
						this.suppressRefresh = false;
					}
				}
			}
		}

		private bool TimeIsValid(TimeSpan time)
		{
			return time >= TimeSpan.Zero && time <= this.Maximum;
		}
	}
}
