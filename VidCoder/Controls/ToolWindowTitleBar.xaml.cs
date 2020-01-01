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
	/// <summary>
	/// Interaction logic for ToolWindowTitleBar.xaml
	/// </summary>
	public partial class ToolWindowTitleBar : UserControl
	{
		public ToolWindowTitleBar()
		{
			InitializeComponent();
		}

		private void OnCloseButtonClick(object sender, RoutedEventArgs e)
		{
			UIUtilities.FindVisualParent<Window>(this).Close();
		}

		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
			"Title",
			typeof(string),
			typeof(ToolWindowTitleBar),
			new PropertyMetadata(new PropertyChangedCallback(OnTitleChanged)));
		public string Title
		{
			get
			{
				return (string)GetValue(TitleProperty);
			}

			set
			{
				SetValue(TitleProperty, value);
			}
		}

		private static void OnTitleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var toolWindowTitleBar = dependencyObject as ToolWindowTitleBar;
			string newText = (string)eventArgs.NewValue;

			toolWindowTitleBar.titleTextBlock.Text = newText;
		}

		public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
			"Icon",
			typeof(ImageSource),
			typeof(ToolWindowTitleBar),
			new PropertyMetadata(null, new PropertyChangedCallback(OnIconChanged)));

		public ImageSource Icon
		{
			get
			{
				return (ImageSource)GetValue(IconProperty);
			}

			set
			{
				SetValue(IconProperty, value);
			}
		}

		private static void OnIconChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var toolWindowTitleBar = dependencyObject as ToolWindowTitleBar;
			ImageSource newImageSource = (ImageSource)eventArgs.NewValue;

			toolWindowTitleBar.iconImage.Source = newImageSource;
			toolWindowTitleBar.iconImage.Visibility = Visibility.Visible;
		}
	}
}
