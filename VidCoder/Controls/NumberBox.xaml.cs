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
	/// Interaction logic for NumberBox.xaml
	/// </summary>
	public partial class NumberBox : UserControl
	{
		private bool allowNegative;
		private string noneCaption;

	    private bool haveFocus = false;

		public NumberBox()
		{
			this.noneCaption = "(none)";
			this.Minimum = int.MinValue;
			this.Maximum = int.MaxValue;
			this.UpdateBindingOnTextChange = true;

			InitializeComponent();

			this.RefreshNumberBox();
		}

		public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(
			"Number",
			typeof(int),
			typeof(NumberBox),
			new PropertyMetadata(new PropertyChangedCallback(OnNumberChanged)));
		public int Number
		{
			get
			{
				return (int)GetValue(NumberProperty);
			}

			set
			{
				SetValue(NumberProperty, value);
			}
		}

		public int Modulus { get; set; }

		public int Minimum { get; set; }
		public int Maximum { get; set; }

		public bool UpdateBindingOnTextChange { get; set; }

		public string NoneCaption
		{
			get
			{
				return this.noneCaption;
			}

			set
			{
				this.noneCaption = value;
				this.RefreshNumberBox();
			}
		}

		public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(
			"AllowEmpty",
			typeof(bool),
			typeof(NumberBox),
			new PropertyMetadata(true, new PropertyChangedCallback(OnAllowEmptyChanged)));
		public bool AllowEmpty
		{
			get
			{
				return (bool)GetValue(AllowEmptyProperty);
			}

			set
			{
				SetValue(AllowEmptyProperty, value);
			}
		}

		public bool AllowNegative
		{
			get
			{
				return this.allowNegative;
			}

			set
			{
				this.allowNegative = value;
				this.RefreshNumberBox();
			}
		}

	    public bool SelectAllOnClick { get; set; }

	    private static void OnNumberChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var numBox = dependencyObject as NumberBox;

			if (!numBox.haveFocus)
			{
				numBox.RefreshNumberBox();
			}
		}

		private static void OnAllowEmptyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var numBox = dependencyObject as NumberBox;
			numBox.RefreshNumberBox();
		}

		private void numberBox_GotFocus(object sender, RoutedEventArgs e)
		{
			this.haveFocus = true;

			if (this.AllowEmpty)
			{
				if (this.Number == 0)
				{
					this.numberBox.Text = string.Empty;
				}

				this.numberBox.Foreground = new SolidColorBrush(Colors.Black);
			}
		}

		private void RefreshNumberBox()
		{
			if (this.AllowEmpty && this.Number == 0)
			{
				this.numberBox.Text = this.NoneCaption;
				this.numberBox.Foreground = new SolidColorBrush(Colors.Gray);
			}
			else
			{
				this.numberBox.Text = this.Number.ToString();
				this.numberBox.Foreground = new SolidColorBrush(Colors.Black);
			}
		}

        private void NumberBoxLostFocus(object sender, RoutedEventArgs e)
        {
            this.haveFocus = false;

            if (this.AllowEmpty && this.numberBox.Text == string.Empty)
            {
                this.Number = 0;
                this.RefreshNumberBox();
                return;
            }

            this.UpdateNumberBindingFromBox();
            this.RefreshNumberBox();
        }

		private void NumberBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			foreach (char c in e.Text)
			{
				if (!char.IsNumber(c) && (!this.AllowNegative || c != '-'))
				{
					e.Handled = true;
					return;
				}
			}
		}

		private void NumberBoxPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				e.Handled = true;
			}
		}

        private void NumberBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.SelectAllOnClick)
            {
                this.Dispatcher.BeginInvoke(new Action(() => this.numberBox.SelectAll()));
            }
        }

		private void NumberBoxTextChanged(object sender, TextChangedEventArgs e)
		{
			if (this.UpdateBindingOnTextChange)
			{
				if (this.AllowEmpty && this.numberBox.Text == string.Empty)
				{
					this.Number = 0;
					return;
				}

				this.UpdateNumberBindingFromBox();
			}
		}

		private void UpdateNumberBindingFromBox()
		{
			int newNumber;
			if (int.TryParse(this.numberBox.Text, out newNumber))
			{
				if (this.NumberIsValid(newNumber))
				{
					if (this.Modulus != 0)
					{
						newNumber = this.GetNearestValue(newNumber, this.Modulus);
					}

					this.Number = newNumber;
				}
			}
		}

		private bool NumberIsValid(int number)
		{
			return (this.AllowNegative || number >= 0) && (number >= this.Minimum) && (number <= this.Maximum);
		}

		private int GetNearestValue(int number, int modulus)
		{
			int remainder = number % modulus;

			if (remainder == 0)
			{
				return number;
			}

			return remainder >= (modulus / 2) ? number + (modulus - remainder) : number - remainder;
		}
	}
}
