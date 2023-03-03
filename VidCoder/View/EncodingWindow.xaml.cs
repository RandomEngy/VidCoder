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
using System.Windows.Shapes;
using System.ComponentModel;
using VidCoder.Extensions;
using VidCoder.Services;
using VidCoder.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Controls.Primitives;
using Microsoft.AnyContainer;
using VidCoder.DragDropUtils;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.View;

using System.Data.SQLite;
using System.Windows.Interop;
using Model;

/// <summary>
/// Interaction logic for EncodingWindow.xaml
/// </summary>
public partial class EncodingWindow : Window
{
	private EncodingWindowViewModel viewModel;

	public EncodingWindow()
	{
		this.InitializeComponent();
		this.listColumn.Width = new GridLength(Config.EncodingListPaneWidth);

		this.DataContextChanged += this.OnDataContextChanged;
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		this.viewModel = e.NewValue as EncodingWindowViewModel;

		if (this.viewModel != null)
		{
			this.SetPanelOpenState(this.viewModel.PresetPanelOpen);
			this.viewModel.PropertyChanged += this.OnPropertyChanged;
		}
	}

	private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (this.viewModel != null && e.PropertyName == nameof(this.viewModel.PresetPanelOpen))
		{
			this.SetPanelOpenState(this.viewModel.PresetPanelOpen);
		}
	}

	private void SetPanelOpenState(bool panelOpen)
	{
		if (panelOpen)
		{
			Grid.SetColumn(this.mainGrid, 1);
			Grid.SetColumnSpan(this.mainGrid, 1);
		}
		else
		{
			Grid.SetColumn(this.mainGrid, 0);
			Grid.SetColumnSpan(this.mainGrid, 2);
		}
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
		{
			Config.EncodingDialogLastTab = this.tabControl.SelectedIndex;
			Config.EncodingListPaneWidth = this.listColumn.ActualWidth;

			transaction.Commit();
		}
	}

	private void ToolBar_Loaded(object sender, RoutedEventArgs e)
	{
		UIUtilities.HideOverflowGrid(sender as ToolBar);
	}

	private void Window_Closed(object sender, EventArgs e)
	{
		if (this.viewModel != null)
		{
			this.viewModel.PropertyChanged -= this.OnPropertyChanged;
		}

		this.DataContext = null;
	}

	private void OnPresetTreeKeyDown(object sender, KeyEventArgs e)
	{
		StaticResolver.Resolve<PresetsService>().HandleKey(e);
	}
}
