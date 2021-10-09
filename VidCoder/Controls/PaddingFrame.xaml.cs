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
using VidCoderCommon.Model;

namespace VidCoder.Controls
{
	/// <summary>
	/// Interaction logic for PaddingFrame.xaml
	/// </summary>
	public partial class PaddingFrame : UserControl
	{
		public PaddingFrame()
		{
			this.InitializeComponent();
		}

		public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
			"Source",
			typeof(ImageSource),
			typeof(PaddingFrame),
			new UIPropertyMetadata(null, OnSourceChanged));
		public ImageSource Source
		{
			get { return (ImageSource)this.GetValue(SourceProperty); }
			set { this.SetValue(SourceProperty, value); }
		}

		private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var control = (PaddingFrame)dependencyObject;
			var source = (ImageSource)eventArgs.NewValue;

			control.image.Source = source;
		}

		public static readonly DependencyProperty OutputSizeProperty = DependencyProperty.Register(
			"OutputSize",
			typeof(OutputSizeInfo),
			typeof(PaddingFrame),
			new UIPropertyMetadata(null, OnOutputSizeChanged));
		public OutputSizeInfo OutputSize
		{
			get { return (OutputSizeInfo)this.GetValue(OutputSizeProperty); }
			set { this.SetValue(OutputSizeProperty, value); }
		}

		private static void OnOutputSizeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var control = (PaddingFrame)dependencyObject;
			var outputSize = (OutputSizeInfo)eventArgs.NewValue;

			if (outputSize == null)
			{
				return;
			}

			control.topPaddingRow.Height = new GridLength(outputSize.Padding.Top, GridUnitType.Star);
			control.contentRow.Height = new GridLength(outputSize.PictureHeight, GridUnitType.Star);
			control.bottomPaddingRow.Height = new GridLength(outputSize.Padding.Bottom, GridUnitType.Star);

			control.leftPaddingColumn.Width = new GridLength(outputSize.Padding.Left, GridUnitType.Star);
			control.contentColumn.Width = new GridLength(outputSize.PictureWidth, GridUnitType.Star);
			control.rightPaddingColumn.Width = new GridLength(outputSize.Padding.Right, GridUnitType.Star);
		}

		public static readonly DependencyProperty PadColorProperty = DependencyProperty.Register(
			"PadColor",
			typeof(Color),
			typeof(PaddingFrame),
			new UIPropertyMetadata(Colors.Black, OnPadColorChanged));
		public Color PadColor
		{
			get { return (Color)this.GetValue(PadColorProperty); }
			set { this.SetValue(PadColorProperty, value); }
		}

		private static void OnPadColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var control = (PaddingFrame)dependencyObject;
			var color = (Color)eventArgs.NewValue;

			control.rootGrid.Background = new SolidColorBrush(color);
		}
	}
}
