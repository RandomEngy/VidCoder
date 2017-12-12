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
using VidCoder.DragDropUtils;
using VidCoder.Services.Windows;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.View
{
	using System.Data.SQLite;
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
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
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

		private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			this.viewModel?.OnSelectedTreeViewPresetChanged(e.NewValue);
		}

		private void OnPresetFolderMenuClick(object sender, RoutedEventArgs e)
		{
			var menuButton = (Button)sender;
			var parentGrid = (Grid)menuButton.Parent;
			ContextMenu folderMenu = ContextMenuService.GetContextMenu(parentGrid);

			folderMenu.PlacementTarget = menuButton;
			folderMenu.Placement = PlacementMode.Bottom;
			folderMenu.IsOpen = true;
		}

		private Point lastPresetTreeViewMouseDown;

		private PresetViewModel draggedPreset;

		private TreeViewItem dropTarget;

		private MoveToFolderAdorner folderMoveAdorner;

		private void OnPresetTreeFolderDragOver(object sender, DragEventArgs e)
		{
			try
			{
				Point currentPosition = e.GetPosition(this.presetTreeView);

				if (DragDropUtilities.IsMovementBigEnough(this.lastPresetTreeViewMouseDown, currentPosition))
				{
					// Verify that this is a valid drop and then store the drop target
					this.DecideDropTarget(e);

					if (this.dropTarget != null)
					{
						this.ShowFolderMoveAdorner(this.dropTarget);
					}
					else
					{
						this.RemoveFolderMoveAdorner();
					}
				}

				e.Handled = true;
			}
			catch (Exception)
			{
			}
		}

		private void OnPresetTreeFolderDrop(object sender, DragEventArgs e)
		{
			try
			{
				e.Effects = DragDropEffects.None;
				e.Handled = true;

				// Verify that this is a valid drop and then store the drop target
				this.DecideDropTarget(e);
				if (this.dropTarget != null)
				{
					Ioc.Get<PresetsService>().MovePresetToFolder(this.draggedPreset, (PresetFolderViewModel)this.dropTarget.Header);
				}

				this.RemoveFolderMoveAdorner();
			}
			catch (Exception)
			{
			}
		}

		private void OnPresetTreeFolderPreviewDragLeave(object sender, DragEventArgs e)
		{
			if (this.dropTarget == null)
			{
				e.Effects = DragDropEffects.None;
			}
			//this.RemoveFolderMoveAdorner();
		}

		private void OnPresetTreeItemMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				this.lastPresetTreeViewMouseDown = e.GetPosition(this.presetTreeView);
			}
		}

		private void OnPresetTreeItemMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				Point currentPosition = e.GetPosition(this.presetTreeView);

				if (DragDropUtilities.IsMovementBigEnough(this.lastPresetTreeViewMouseDown, currentPosition))
				{
					this.draggedPreset = this.presetTreeView.SelectedItem as PresetViewModel;

					if (this.draggedPreset != null)
					{
						var windowManager = Ioc.Get<IWindowManager>();

						windowManager.SuspendDropOnWindows();
						DragDrop.DoDragDrop((DependencyObject)sender, this.presetTreeView.SelectedItem, DragDropEffects.Move);
						windowManager.ResumeDropOnWindows();
					}
				}
			}
		}

		private void ShowFolderMoveAdorner(TreeViewItem item)
		{
			if (this.folderMoveAdorner == null)
			{
				Border borderChild = UIUtilities.FindDescendant<Border>(item, border => border.Name == "Bd");

				var adornerLayer = AdornerLayer.GetAdornerLayer(borderChild);
				this.folderMoveAdorner = new MoveToFolderAdorner(borderChild, adornerLayer);
			}
		}

		private void RemoveFolderMoveAdorner()
		{
			if (this.folderMoveAdorner != null)
			{
				this.folderMoveAdorner.Detach();
				this.folderMoveAdorner = null;
			}
		}

		private void DecideDropTarget(DragEventArgs e)
		{
			TreeViewItem item = this.GetNearestContainer(e.OriginalSource as UIElement);
			bool isValidDropTarget = this.CheckDropTarget(item.Header);

			if (isValidDropTarget)
			{
				this.dropTarget = item;
				e.Effects = DragDropEffects.Move;
			}
			else
			{
				this.dropTarget = null;
				e.Effects = DragDropEffects.None;
			}
		}

		private bool CheckDropTarget(object targetItem)
		{
			if (this.draggedPreset == null)
			{
				return false;
			}

			var targetFolder = targetItem as PresetFolderViewModel;
			if (targetFolder == null)
			{
				return false;
			}

			if (targetFolder.IsBuiltIn)
			{
				return false;
			}

			if (this.draggedPreset.Preset.FolderId == targetFolder.Id)
			{
				return false;
			}

			return true;
		}

		private TreeViewItem GetNearestContainer(UIElement element)
		{
			// Walk up the element tree to the nearest tree view item.
			TreeViewItem container = element as TreeViewItem;
			while (container == null && element != null)
			{
				element = VisualTreeHelper.GetParent(element) as UIElement;
				container = element as TreeViewItem;
			}
			return container;
		}
	}
}
