using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VidCoder.Resources;

namespace VidCoder.Controls
{
	/// <summary>
	/// Interaction logic for NumberBox.xaml
	/// </summary>
	public partial class NumberBox : UserControl
	{
		private static readonly TimeSpan SelectAllThreshold = TimeSpan.FromMilliseconds(500);

		private RefireControl refireControl;

		private string noneCaption;
		private bool hasFocus;
		private DateTimeOffset lastFocusMouseDown;
		private bool suppressRefreshFromNumberChange;
		private bool suppressUpdateFromTextChange;
		private readonly char decimalSeparator = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

		public NumberBox()
		{
			this.noneCaption = "(none)";
			this.UpdateBindingOnTextChange = true;
			this.ShowIncrementButtons = true;
			this.SelectAllOnClick = true;

			this.InitializeComponent();

			var automationNameDescriptor = DependencyPropertyDescriptor.FromProperty(AutomationProperties.NameProperty, typeof(NumberBox));
			automationNameDescriptor.AddValueChanged(this, (sender, args) =>
			{
				string newName = (string)this.GetValue(AutomationProperties.NameProperty);

				if (!string.IsNullOrEmpty(newName))
				{
					this.numberBox.SetValue(AutomationProperties.NameProperty, newName);

					this.increaseButton.SetValue(AutomationProperties.NameProperty, string.Format(CultureInfo.CurrentCulture, CommonRes.NumberBoxIncreaseButtonAutomationTextFormat, newName));
					this.decreaseButton.SetValue(AutomationProperties.NameProperty, string.Format(CultureInfo.CurrentCulture, CommonRes.NumberBoxDecreaseButtonAutomationTextFormat, newName));
				}
			});

			var automationHelpTextDescriptor = DependencyPropertyDescriptor.FromProperty(AutomationProperties.HelpTextProperty, typeof(NumberBox));
			automationHelpTextDescriptor.AddValueChanged(this, (sender, args) =>
			{
				string newName = (string)this.GetValue(AutomationProperties.HelpTextProperty);

				if (!string.IsNullOrEmpty(newName))
				{
					this.numberBox.SetValue(AutomationProperties.HelpTextProperty, newName);
				}
			});

			this.RefreshNumberBox();
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (!this.ShowIncrementButtons)
			{
				this.incrementButtonsGrid.Visibility = Visibility.Collapsed;
			}
		}

		public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(
			"Number",
			typeof(double),
			typeof(NumberBox),
			new PropertyMetadata(OnNumberChanged));
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


		public const string MinimumPropertyName = "Minimum";
		public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
			MinimumPropertyName,
			typeof(double),
			typeof(NumberBox),
			new UIPropertyMetadata(double.MinValue));
		public double Minimum
		{
			get
			{
				return (double)GetValue(MinimumProperty);
			}
			set
			{
				SetValue(MinimumProperty, value);
			}
		}

		public const string MaximumPropertyName = "Maximum";
		public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
			MaximumPropertyName,
			typeof(double),
			typeof(NumberBox),
			new UIPropertyMetadata(double.MaxValue));
		public double Maximum
		{
			get
			{
				return (double)GetValue(MaximumProperty);
			}
			set
			{
				SetValue(MaximumProperty, value);
			}
		}

		public const string ModulusPropertyName = "Modulus";
		public static readonly DependencyProperty ModulusProperty = DependencyProperty.Register(
			ModulusPropertyName,
			typeof(double),
			typeof(NumberBox),
			new UIPropertyMetadata(0.0));
		public double Modulus
		{
			get
			{
				return (double)GetValue(ModulusProperty);
			}
			set
			{
				SetValue(ModulusProperty, value);
			}
		}

		public static readonly DependencyProperty BoxForegroundProperty = DependencyProperty.Register(
			"BoxForeground",
			typeof (Brush),
			typeof (NumberBox),
			new PropertyMetadata(OnBoxForegroundChanged));
		public Brush BoxForeground
		{
			get
			{
				return (Brush)GetValue(BoxForegroundProperty);
			}

			set
			{
				SetValue(BoxForegroundProperty, value);
			}
		}

		public bool UpdateBindingOnTextChange { get; set; }

		public bool ShowIncrementButtons { get; set; }

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

		public bool SelectAllOnClick { get; set; }

		private static void OnNumberChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			if (eventArgs.NewValue != eventArgs.OldValue)
			{
				var numBox = dependencyObject as NumberBox;

				if (!numBox.suppressRefreshFromNumberChange)
				{
					numBox.RefreshNumberBox();
				}
			}
		}

		private static void OnBoxForegroundChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			if (eventArgs.NewValue != eventArgs.OldValue)
			{
				var numBox = dependencyObject as NumberBox;
				numBox.RefreshNumberBoxColor();
			}
		}

		private static void OnAllowEmptyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var numBox = dependencyObject as NumberBox;
			numBox.RefreshNumberBox();
		}

		private double Increment
		{
			get
			{
				return this.Modulus > 0 ? this.Modulus : 1;
			}
		}

		private void RefreshNumberBox()
		{
			this.suppressUpdateFromTextChange = true;
			if (this.AllowEmpty && this.Number == 0)
			{
				this.numberBox.Text = this.hasFocus ? string.Empty : this.NoneCaption;
			}
			else
			{
				this.numberBox.Text = this.Number.ToString();
			}

			this.suppressUpdateFromTextChange = false;

			this.RefreshNumberBoxColor();
		}

		private void RefreshNumberBoxColor()
		{
			if (this.numberBox.Text == this.NoneCaption)
			{
				this.numberBox.Foreground = SystemColors.GrayTextBrush;
			}
			else
			{
				if (this.BoxForeground != null)
				{
					this.numberBox.Foreground = this.BoxForeground;
				}
				else
				{
					this.numberBox.SetResourceReference(Control.ForegroundProperty, "ControlTextBrush");
				}
			}
		}

		private void UpButtonMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.refireControl = new RefireControl(this.IncrementNumber);
			this.refireControl.Begin();
		}

		private void UpButtonMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.refireControl?.Stop();
		}

		private void DownButtonMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.refireControl = new RefireControl(this.DecrementNumber);
			this.refireControl.Begin();
		}

		private void DownButtonMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.refireControl?.Stop();
		}

		private void IncrementNumber()
		{
			double newNumber;
			if (this.AllowEmpty && this.Number == 0)
			{
				newNumber = Math.Max(this.Minimum, this.Increment);
			}
			else if (this.AllowEmpty && this.Number == this.Maximum && this.Maximum < 0)
			{
				newNumber = 0;
			}
			else
			{
				newNumber = this.Number + this.Increment;
			}

			if (newNumber > this.Maximum && (!this.AllowEmpty || newNumber != 0))
			{
				newNumber = this.Maximum;
			}

			if (newNumber != this.Number)
			{
				this.Number = newNumber;
			}
		}

		private void DecrementNumber()
		{
			double newNumber;
			if (this.AllowEmpty && this.Number == 0)
			{
				newNumber = Math.Min(this.Maximum, -this.Increment);
			}
			else if (this.AllowEmpty && this.Number == this.Minimum && this.Minimum > 0)
			{
				newNumber = 0;
			}
			else
			{
				newNumber = this.Number - this.Increment;
			}

			if (newNumber < this.Minimum && (!this.AllowEmpty || newNumber != 0))
			{
				newNumber = this.Minimum;
			}

			if (newNumber != this.Number)
			{
				this.Number = newNumber;
			}
		}

		private void NumberBoxGotFocus(object sender, RoutedEventArgs e)
		{
			this.hasFocus = true;

			if (this.AllowEmpty)
			{
				if (this.Number == 0)
				{
					this.numberBox.Text = string.Empty;
				}

				if (this.BoxForeground != null)
				{
					this.numberBox.Foreground = this.BoxForeground;
				}
				else
				{
					this.numberBox.SetResourceReference(Control.ForegroundProperty, "ControlTextBrush");
				}
			}
		}

		private void NumberBoxLostFocus(object sender, RoutedEventArgs e)
		{
			this.hasFocus = false;

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
				if (!char.IsNumber(c) && c != decimalSeparator && (this.Minimum >= 0 || c != '-'))
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
			else if (e.Key == Key.Up)
			{
				this.IncrementNumber();
			}
			else if (e.Key == Key.Down)
			{
				this.DecrementNumber();
			}
		}

		private void NumberBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (!this.hasFocus)
			{
				this.lastFocusMouseDown = DateTimeOffset.UtcNow;
			}
		}

		private void NumberBoxPreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			// If this mouse up is soon enough after an initial click on the box, select all.
			if (this.SelectAllOnClick && DateTimeOffset.UtcNow - this.lastFocusMouseDown < SelectAllThreshold)
			{
				this.Dispatcher.BeginInvoke(new Action(() => this.numberBox.SelectAll()));
			}
		}

		private void NumberBoxTextChanged(object sender, TextChangedEventArgs e)
		{
			if (this.suppressUpdateFromTextChange)
			{
				return;
			}

			if (this.UpdateBindingOnTextChange)
			{
				if (this.AllowEmpty && this.numberBox.Text == string.Empty)
				{
					this.Number = 0;
					return;
				}

				this.UpdateNumberBindingFromBox();
			}

			this.RefreshNumberBoxColor();
		}

		private void UpdateNumberBindingFromBox()
		{
			double newNumber;
			if (double.TryParse(this.numberBox.Text, out newNumber))
			{
				if (this.NumberIsValid(newNumber))
				{
					if (this.Modulus != 0)
					{
						newNumber = this.GetNearestValue(newNumber, this.Modulus);
					}

					if (newNumber != this.Number)
					{
						// While updating the binding we don't need to react to the change.
						this.suppressRefreshFromNumberChange = true;
						this.Number = newNumber;
						this.suppressRefreshFromNumberChange = false;
					}
				}
			}
		}

		private bool NumberIsValid(double number)
		{
			return number >= this.Minimum && number <= this.Maximum;
		}

		private double GetNearestValue(double number, double modulus)
		{
			double remainder = number % modulus;

			if (remainder == 0)
			{
				return number;
			}

			return remainder >= (modulus / 2) ? number + (modulus - remainder) : number - remainder;
		}
	}
}
