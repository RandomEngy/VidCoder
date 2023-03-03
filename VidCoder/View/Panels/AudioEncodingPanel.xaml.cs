using ReactiveUI;
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
using VidCoder.ViewModel;

namespace VidCoder.View.Panels;

/// <summary>
/// Interaction logic for AudioEncodingPanel.xaml
/// </summary>
public partial class AudioEncodingPanel : UserControl
{
	private AudioEncodingViewModel viewModel;

	public AudioEncodingPanel()
	{
		InitializeComponent();

		this.DataContextChanged += this.OnDataContextChanged;
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		this.viewModel = (AudioEncodingViewModel)this.DataContext;
		this.viewModel.WhenAnyValue(x => x.DrcEnabled).Subscribe(enabled =>
		{
			this.drcLabel.SetResourceReference(
				Control.ForegroundProperty,
				enabled ? SystemColors.ControlTextBrushKey : SystemColors.GrayTextBrushKey);
		});
	}
}
