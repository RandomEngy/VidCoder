using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VidCoder.ViewModel;

namespace VidCoder.Controls
{
    public class PresetTreeView : TreeView
    {
	    protected override DependencyObject GetContainerForItemOverride()
	    {
		    var childItem = new PresetTreeViewItem();

			childItem.OnHierarchyMouseUp += this.OnChildItemMouseLeftButtonUp;

		    return childItem;
	    }

	    private void OnChildItemMouseLeftButtonUp(object sender, MouseEventArgs e)
	    {
		    this.OnHierarchyMouseUp?.Invoke(this, e);
	    }

	    public event MouseEventHandler OnHierarchyMouseUp;
	}

	public class PresetTreeViewItem : TreeViewItem
	{
		public PresetTreeViewItem()
		{
			this.MouseLeftButtonUp += this.OnMouseLeftButtonUp;
			this.Selected += (sender, args) =>
			{
				if (!object.ReferenceEquals(sender, args.OriginalSource))
				{
					return;
				}

				var item = args.OriginalSource as TreeViewItem;
				if (item?.Header is PresetViewModel)
				{
					UIUtilities.BringIntoView(item);
				}
			};
		}

		private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.OnHierarchyMouseUp?.Invoke(this, e);
		}

		protected override DependencyObject GetContainerForItemOverride()
		{
			var childItem = new PresetTreeViewItem();

			childItem.MouseLeftButtonUp += this.OnMouseLeftButtonUp;

			return childItem;
		}

		public event MouseEventHandler OnHierarchyMouseUp;
	}
}
