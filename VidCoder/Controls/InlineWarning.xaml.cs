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

namespace VidCoder.Controls;

/// <summary>
/// Interaction logic for InlineWarning.xaml
/// </summary>
public partial class InlineWarning : UserControl
{
	public InlineWarning()
	{
		InitializeComponent();
	}

	public static readonly DependencyProperty WarningTextProperty = DependencyProperty.Register(
		"WarningText",
		typeof(string),
		typeof(InlineWarning),
		new PropertyMetadata(new PropertyChangedCallback(OnWarningTextChanged)));
	public string WarningText
	{
		get
		{
			return (string)GetValue(WarningTextProperty);
		}

		set
		{
			SetValue(WarningTextProperty, value);
		}
	}

	private static void OnWarningTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
	{
		var inlineWarning = dependencyObject as InlineWarning;
		string newText = (string)eventArgs.NewValue;

		inlineWarning.warningText.Text = newText;
	}
}
