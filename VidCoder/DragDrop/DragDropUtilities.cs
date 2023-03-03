﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections;
using System.Reflection;
using System.Windows;
using DynamicData;
using Expression = System.Linq.Expressions.Expression;

namespace VidCoder.DragDropUtils;

public static class DragDropUtilities
{
	// Walks up the tree starting at the bottomMostVisual, until it finds the first item container for the ItemsControl
	// passed as a parameter.
	// In order to make sure it works with any control that derives from ItemsControl, this method makes no assumption 
	// about the type of that container.(it will get a ListBoxItem if it's a ListBox, a ListViewItem if it's a ListView...)
	public static FrameworkElement GetItemContainer(ItemsControl itemsControl, Visual bottomMostVisual)
	{
		FrameworkElement itemContainer = null;
		if (itemsControl != null && bottomMostVisual != null && itemsControl.Items.Count >= 1)
		{
			var someContainer = itemsControl.ItemContainerGenerator.ContainerFromIndex(0);
			for (int i = 1; i < itemsControl.Items.Count; i++)
			{
				if (someContainer != null)
				{
					break;
				}

				someContainer = itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
			}

			if (someContainer != null)
			{
				Type containerType = someContainer.GetType();

				itemContainer = FindAncestor(containerType, bottomMostVisual);

				// Make sure that the container found belongs to the items control passed as a parameter.
				if (itemContainer != null && itemContainer.DataContext != null)
				{
					FrameworkElement itemContainerVerify = itemsControl.ItemContainerGenerator.ContainerFromItem(itemContainer.DataContext) as FrameworkElement;
					if (itemContainer != itemContainerVerify)
					{
						itemContainer = null;
					}
				}
			}
		}
		return itemContainer;
	}

	public static FrameworkElement FindAncestor(Type ancestorType, Visual visual)
	{
		while (visual != null && !ancestorType.IsInstanceOfType(visual))
		{
			visual = (Visual)VisualTreeHelper.GetParent(visual);
		}
		return visual as FrameworkElement;
	}


	// Finds the orientation of the panel of the ItemsControl that contains the itemContainer passed as a parameter.
	// The orientation is needed to figure out where to draw the adorner that indicates where the item will be dropped.
	public static bool HasVerticalOrientation(FrameworkElement itemContainer)
	{
		bool hasVerticalOrientation = true;
		if (itemContainer != null)
		{
			Panel panel = VisualTreeHelper.GetParent(itemContainer) as Panel;
			StackPanel stackPanel;
			WrapPanel wrapPanel;

			if ((stackPanel = panel as StackPanel) != null)
			{
				hasVerticalOrientation = stackPanel.Orientation == Orientation.Vertical;
			}
			else if ((wrapPanel = panel as WrapPanel) != null)
			{
				hasVerticalOrientation = wrapPanel.Orientation == Orientation.Vertical;
			}
			// You can add support for more panel types here.
		}
		return hasVerticalOrientation;
	}

	public static void InsertItemsInItemsControl(ItemsControl itemsControl, object sourceList, List<object> itemsToInsert, int insertionIndex)
	{
		if (itemsToInsert != null)
		{
			if (sourceList != null)
			{
				// If we have an associated SourceList, insert the items into there as it's the master list
				Type sourceListType = sourceList.GetType();

				Action<dynamic> action = innerList =>
				{
					for (int i = itemsToInsert.Count - 1; i >= 0; i--)
					{
						dynamic itemAsDynamic = itemsToInsert[i];
						innerList.Insert(insertionIndex, itemAsDynamic);
					}
				};

				MethodInfo editMethod = sourceListType.GetMethod("Edit");
				editMethod.Invoke(sourceList, new[] {action});
			}
			else
			{
				// Otherwise check the ItemsSource
				IEnumerable itemsSource = itemsControl.ItemsSource;

				for (int i = itemsToInsert.Count - 1; i >= 0; i--)
				{
					if (itemsSource == null)
					{
						itemsControl.Items.Insert(insertionIndex, itemsToInsert[i]);
					}
					// Is the ItemsSource IList or IList<T>? If so, insert the dragged item in the list.
					else if (itemsSource is IList)
					{
						((IList)itemsSource).Insert(insertionIndex, itemsToInsert[i]);
					}
					else
					{
						Type type = itemsSource.GetType();
						Type genericIListType = type.GetInterface("IList`1");
						if (genericIListType != null)
						{
							type.GetMethod("Insert").Invoke(itemsSource, new object[] { insertionIndex, itemsToInsert[i] });
						}
					}
				}
			}
		}
	}

	public static List<int> RemoveItemsFromItemsControl(ItemsControl itemsControl, object sourceList, List<object> itemsToRemove)
	{
		var indiciesToBeRemoved = new List<int>();
		if (itemsToRemove != null)
		{
			indiciesToBeRemoved.AddRange(itemsToRemove.Select(t => itemsControl.Items.IndexOf(t)).Where(t2 => t2 >= 0));
			indiciesToBeRemoved.Sort();

			if (sourceList != null)
			{
				// If we have an associated SourceList, remove the items from there as it's the master list
				Type sourceListType = sourceList.GetType();

				Action<dynamic> action = innerList =>
				{
					for (int i = indiciesToBeRemoved.Count - 1; i >= 0; i--)
					{
						innerList.RemoveAt(indiciesToBeRemoved[i]);
					}
				};

				MethodInfo editMethod = sourceListType.GetMethod("Edit");
				editMethod.Invoke(sourceList, new[] {action});
			}
			else
			{
				// Otherwise check the ItemsSource
				for (int i = indiciesToBeRemoved.Count - 1; i >= 0; i--)
				{
					IEnumerable itemsSource = itemsControl.ItemsSource;
					if (itemsSource == null)
					{
						itemsControl.Items.RemoveAt(indiciesToBeRemoved[i]);
					}
					// Is the ItemsSource IList or IList<T>? If so, remove the item from the list.
					else if (itemsSource is IList)
					{
						((IList)itemsSource).RemoveAt(indiciesToBeRemoved[i]);
					}
					else
					{
						Type type = itemsSource.GetType();
						Type genericIListType = type.GetInterface("IList`1");
						if (genericIListType != null)
						{
							type.GetMethod("RemoveAt").Invoke(itemsSource, new object[] { indiciesToBeRemoved[i] });
						}
					}
				}
			}
		}

		return indiciesToBeRemoved;
	}

	public static bool IsInFirstHalf(FrameworkElement container, Point clickedPoint, bool hasVerticalOrientation)
	{
		if (hasVerticalOrientation)
		{
			return clickedPoint.Y < container.ActualHeight / 2;
		}
		return clickedPoint.X < container.ActualWidth / 2;
	}

	public static bool IsMovementBigEnough(Point initialMousePosition, Point currentPosition)
	{
		return (Math.Abs(currentPosition.X - initialMousePosition.X) >= SystemParameters.MinimumHorizontalDragDistance ||
			 Math.Abs(currentPosition.Y - initialMousePosition.Y) >= SystemParameters.MinimumVerticalDragDistance);
	}
}
