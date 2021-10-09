using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VidCoder.Extensions;

namespace VidCoder.Controls
{
	public partial class VideoSeekBar : UserControl
	{
		public VideoSeekBar()
		{
			this.InitializeComponent();
		}

		public enum SeekBarColorChoice
		{
			Green,
			Blue
		}

		public static readonly DependencyProperty VideoDurationProperty = DependencyProperty.Register(
			"VideoDuration",
			typeof(TimeSpan),
			typeof(VideoSeekBar),
			new UIPropertyMetadata(TimeSpan.Zero, OnTimeChanged));
		public TimeSpan VideoDuration
		{
			get { return (TimeSpan)this.GetValue(VideoDurationProperty); }
			set { this.SetValue(VideoDurationProperty, value); }
		}

		public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
			"Position",
			typeof(TimeSpan),
			typeof(VideoSeekBar),
			new UIPropertyMetadata(TimeSpan.Zero, OnTimeChanged));
		public TimeSpan Position
		{
			get { return (TimeSpan)this.GetValue(PositionProperty); }
			set { this.SetValue(PositionProperty, value); }
		}

		private static void OnTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			((VideoSeekBar)dependencyObject).RefreshBar();
		}

		public static readonly DependencyProperty SeekBarColorProperty = DependencyProperty.Register(
			"SeekBarColor",
			typeof(SeekBarColorChoice),
			typeof(VideoSeekBar),
			new UIPropertyMetadata(SeekBarColorChoice.Green, OnSeekBarColorChanged));
		public SeekBarColorChoice SeekBarColor
		{
			get => (SeekBarColorChoice)this.GetValue(SeekBarColorProperty);
			set => this.SetValue(SeekBarColorProperty, value);
		}

		private static void OnSeekBarColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			((VideoSeekBar)dependencyObject).UpdateSeekBarColor((SeekBarColorChoice)eventArgs.NewValue);
		}

		private void UpdateSeekBarColor(SeekBarColorChoice seekBarColor)
		{
			var gradientStopCollection = new GradientStopCollection();
			Color startColor = seekBarColor == SeekBarColorChoice.Green ? Color.FromRgb(0x3D, 0xAD, 0x35) : Color.FromRgb(0x25, 0x84, 0xC4);
			Color endColor = seekBarColor == SeekBarColorChoice.Green ? Color.FromRgb(0x28, 0x72, 0x23) : Color.FromRgb(0x1A, 0x5D, 0x89);

			gradientStopCollection.Add(new GradientStop(startColor, 0.0));
			gradientStopCollection.Add(new GradientStop(endColor, 1.0));
			this.barRectangle.Fill = new LinearGradientBrush(gradientStopCollection, new Point(0, 0), new Point(0, 1));
		}

		private void RefreshBar()
		{
			double controlWidth = this.ActualWidth;

			if (this.VideoDuration <= TimeSpan.Zero || this.Position <= TimeSpan.Zero)
			{
				this.barRectangle.Width = 0;
				return;
			}

			if (this.Position >= this.VideoDuration)
			{
				this.barRectangle.Width = controlWidth;
				return;
			}

			this.barRectangle.Width = ((double) this.Position.Ticks / this.VideoDuration.Ticks) * controlWidth;

			if (this.EnableTimeText)
			{
				this.timeText.Text = this.Position.FormatShort() + " / " + this.VideoDuration.FormatShort();
			}
		}

		private bool enableTimeText;
		public bool EnableTimeText
		{
			get => this.enableTimeText;
			set
			{
				this.enableTimeText = value;
				if (value)
				{
					this.timeText.Visibility = Visibility.Visible;
				}
				else
				{
					this.timeText.Visibility = Visibility.Collapsed;
				}
			}
		}

		private void OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshBar();
		}

		private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.HandleMouseEvent(e);
			this.CaptureMouse();
			e.Handled = true;
		}

		private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.ReleaseMouseCapture();
			e.Handled = true;
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				this.HandleMouseEvent(e);
			}
		}

		private void HandleMouseEvent(MouseEventArgs e)
		{
			if (this.VideoDuration <= TimeSpan.Zero)
			{
				return;
			}


			Point location = e.GetPosition(this);

			double fractionOfBar = location.X / this.ActualWidth;
			if (fractionOfBar < 0)
			{
				fractionOfBar = 0;
			}

			if (fractionOfBar > 1)
			{
				fractionOfBar = 1;
			}

			long newPositionTicks = (long)(this.VideoDuration.Ticks * fractionOfBar);
			TimeSpan newPosition = TimeSpan.FromTicks(newPositionTicks);

			this.Position = newPosition;
		}
	}
}
