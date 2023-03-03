using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace VidCoder;

using System.Windows.Media;

public static class UIUtilities
{
	private const double ScrollBarThickness = 18;

	public static void HideOverflowGrid(ToolBar toolBar)
	{
		var overflowGrid = toolBar.Template.FindName("OverflowGrid", toolBar) as FrameworkElement;
		if (overflowGrid != null)
		{
			overflowGrid.Visibility = Visibility.Collapsed;
		}
	}

	/// <summary>
	/// Finds a parent of a given item on the visual tree.
	/// </summary>
	/// <typeparam name="T">The type of the queried item.</typeparam>
	/// <param name="child">A direct or indirect child of the queried item.</param>
	/// <returns>The first parent item that matches the submitted type parameter. 
	/// If not matching item can be found, a null reference is being returned.</returns>
	public static T FindVisualParent<T>(DependencyObject child)
		where T : DependencyObject
	{
		// get parent item
		DependencyObject parentObject = VisualTreeHelper.GetParent(child);

		// we’ve reached the end of the tree
		if (parentObject == null) return null;

		// check if the parent matches the type we’re looking for
		T parent = parentObject as T;
		if (parent != null)
		{
			return parent;
		}
		else
		{
			// use recursion to proceed with next level
			return FindVisualParent<T>(parentObject);
		}
	}

	/// <summary>
	/// Finds a descendant of the given item that fits the given criteria.
	/// </summary>
	/// <typeparam name="T">The type of the item to find.</typeparam>
	/// <param name="element"></param>
	/// <param name="searchFunc">A function to use to search fo the element.</param>
	/// <returns>The element if found, or null.</returns>
	public static T FindDescendant<T>(DependencyObject element, Func<T, bool> searchFunc = null)
		where T : DependencyObject
	{
		var elementTyped = element as T;
		if (elementTyped != null)
		{
			if (searchFunc == null || searchFunc(elementTyped))
			{
				return elementTyped;
			}
		}

		int childCount = VisualTreeHelper.GetChildrenCount(element);
		for (int i = 0; i < childCount; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(element, i);
			T childResult = FindDescendant<T>(child, searchFunc);
			if (childResult != null)
			{
				return childResult;
			}
		}

		return null;
	}

	/// <summary>
	/// Brings the given element into view in its parent ScrollViewer.
	/// </summary>
	/// <param name="element">The element to scroll to.</param>
	public static void BringIntoView(FrameworkElement element)
	{
		if (element == null)
		{
			return;
		}
		
		ScrollViewer scrollViewer = UIUtilities.FindVisualParent<ScrollViewer>(element);

		if (scrollViewer == null)
		{
			return;
		}

		double viewerHeight = scrollViewer.ActualHeight;
		double elementHeight = element.ActualHeight;

		double currentViewStart = scrollViewer.VerticalOffset;
		double currentViewEnd = currentViewStart + viewerHeight;

		var scrollViewerContent = scrollViewer.Content as FrameworkElement;

		if (scrollViewerContent == null)
		{
			return;
		}

		var transform = element.TransformToVisual(scrollViewerContent);

		var positionInScrollViewer = transform.Transform(new Point(0, 0));
		double itemStart = positionInScrollViewer.Y;
		double itemEnd = itemStart + elementHeight;

		if (itemStart < currentViewStart)
		{
			scrollViewer.ScrollToVerticalOffset(itemStart);
		}
		else if (itemEnd > currentViewEnd - ScrollBarThickness)
		{
			if (element.ActualHeight > viewerHeight - ScrollBarThickness)
			{
				scrollViewer.ScrollToVerticalOffset(itemStart);
			}
			else
			{
				scrollViewer.ScrollToVerticalOffset(itemEnd - viewerHeight + ScrollBarThickness);
			}
		}
	}
}
