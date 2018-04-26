using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using VidCoder.DragDropUtils;
using VidCoder.Extensions;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.Controls
{
    /// <summary>
    /// Interaction logic for PresetTreeViewContainer.xaml
    /// </summary>
    public partial class PresetTreeViewContainer : UserControl
    {
	    private PresetsService presetsService = Ioc.Get<PresetsService>();

        public PresetTreeViewContainer()
        {
            this.InitializeComponent();

	        this.presetsService.PresetFolderManuallyExpanded += (sender, model) =>
	        {
		        var item =  UIUtilities.FindDescendant<TreeViewItem>(this.presetTreeView, viewItem =>
		        {
			        return viewItem.Header == model;
		        });

		        if (item != null)
		        {
					DispatchUtilities.BeginInvoke(
						() =>
						{
							UIUtilities.BringIntoView(item);
						},
						DispatcherPriority.Background);
		        }
	        };
        }

	    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	    {
		    object selectedItem = e.NewValue;

		    if (selectedItem == null || selectedItem is PresetFolderViewModel)
		    {
			    // If it's a folder that got "selected" we ignore it and leave the previous preset selected for real
			    return;
		    }

		    if (PresetComboBox.PresetComboOpen)
		    {
				// If inside a combo box the combo box binding on close will handle it.
			    return;
		    }

			DispatchUtilities.BeginInvoke(() =>
			{
				// This might be in the layout phase. Invoke on dispatcher to process cleanly.
				this.presetsService.SelectedPreset = (PresetViewModel)selectedItem;
			});
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

	    public PresetTreeView PresetTreeView => this.presetTreeView;

		public bool EnableFolderActions { get; set; }

	    private Point lastPresetTreeViewMouseDown;

	    private PresetViewModel draggedPreset;

	    private TreeViewItem dropTarget;

	    private MoveToFolderAdorner folderMoveAdorner;
	    private TreeViewItem adornedItem;

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
		    if (item != this.adornedItem)
		    {
			    this.RemoveFolderMoveAdorner();
		    }

		    if (this.folderMoveAdorner == null)
		    {
			    Border borderChild = UIUtilities.FindDescendant<Border>(item, border => border.Name == "Bd");

			    var adornerLayer = AdornerLayer.GetAdornerLayer(borderChild);
			    this.folderMoveAdorner = new MoveToFolderAdorner(borderChild, adornerLayer);
			    this.adornedItem = item;
		    }
	    }

	    private void RemoveFolderMoveAdorner()
	    {
		    if (this.folderMoveAdorner != null)
		    {
			    this.folderMoveAdorner.Detach();
			    this.folderMoveAdorner = null;
			    this.adornedItem = null;
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

	    private void OnRequestBringPresetIntoView(object sender, RequestBringIntoViewEventArgs e)
	    {
		    e.Handled = true;
	    }
	}
}
