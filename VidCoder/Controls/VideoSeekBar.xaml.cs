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

namespace VidCoder.Controls
{
	public partial class VideoSeekBar : UserControl
	{
		public VideoSeekBar()
		{
			this.InitializeComponent();
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
		}

		private void OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshBar();
		}
	}
}
