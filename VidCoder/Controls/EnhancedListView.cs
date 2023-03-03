using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace VidCoder.Controls;

public class EnhancedListView : ListView
{
	private readonly List<IListItemViewModel> selectedItems = new List<IListItemViewModel>();

	protected override void OnSelectionChanged(SelectionChangedEventArgs e)
	{
		base.OnSelectionChanged(e);

		bool isVirtualizing = VirtualizingPanel.GetIsVirtualizing(this);
		bool isMultiSelect = this.SelectionMode != SelectionMode.Single;

		if (isVirtualizing && isMultiSelect)
		{
			var newSelectedItems = this.SelectedItems.Cast<IListItemViewModel>().ToList();

			foreach (var deselectedItem in this.selectedItems.Except(newSelectedItems))
			{
				deselectedItem.IsSelected = false;
			}

			this.selectedItems.Clear();
			this.selectedItems.AddRange(newSelectedItems);

			foreach (var newlySelectedItem in this.selectedItems)
			{
				newlySelectedItem.IsSelected = true;
			}
		}
	}
}
