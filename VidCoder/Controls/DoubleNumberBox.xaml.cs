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
	/// Interaction logic for DoubleNumberBox.xaml
	/// </summary>
	public partial class DoubleNumberBox : UserControl
	{
		private bool haveFocus = false;

		public DoubleNumberBox()
		{
			this.Minimum = double.MinValue;
			this.Maximum = double.MaxValue;

			this.AllowEmpty = true;
			this.UpdateBindingOnTextChange = true;

			InitializeComponent();

			this.RefreshNumberBox();
		}

		public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(
			"Number",
			typeof(double),
			typeof(DoubleNumberBox),
			new PropertyMetadata(new PropertyChangedCallback(OnNumberChanged)));
		public double Number
		{
			get
			{
				return (double)GetValue(NumberProperty);
			}

			set
			{
				SetValue(NumberProperty, value);
			}
		}

		public double Minimum { get; set; }
		public double Maximum { get; set; }

		public bool UpdateBindingOnTextChange { get; set; }
		public bool AllowEmpty { get; set; }

		private static void OnNumberChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var numBox = dependencyObject as DoubleNumberBox;
			double newNumber = (double)eventArgs.NewValue;

			if (!numBox.haveFocus)
			{
				numBox.RefreshNumberBox();
			}
		}

		private void numberBox_GotFocus(object sender, RoutedEventArgs e)
		{
			this.haveFocus = true;

			if (this.AllowEmpty && this.Number == 0)
			{
				this.numberBox.Text = string.Empty;
			}
		}

		private void numberBox_LostFocus(object sender, RoutedEventArgs e)
		{
			this.haveFocus = false;

			this.UpdateNumberBindingFromBox();
			this.RefreshNumberBox();
		}

		private void RefreshNumberBox()
		{
			this.numberBox.Text = this.Number.ToString();
		}

		private void numberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			foreach (char c in e.Text)
			{
				if (!char.IsNumber(c) && c != '.')
				{
					e.Handled = true;
					return;
				}
			}
		}

		private void numberBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				e.Handled = true;
			}
		}

		private void numberBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (this.UpdateBindingOnTextChange)
			{
				this.UpdateNumberBindingFromBox();
			}
		}

		private void UpdateNumberBindingFromBox()
		{
			double newNumber;
			if (double.TryParse(this.numberBox.Text, out newNumber))
			{
				if (newNumber >= this.Minimum && newNumber <= this.Maximum)
				{
					this.Number = newNumber;
				}
			}
		}
	}
}
