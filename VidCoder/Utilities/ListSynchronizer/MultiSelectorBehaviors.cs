using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;

namespace VidCoder
{
	/// <summary>
	/// A sync behaviour for a multiselector.
	/// </summary>
	public static class MultiSelectorBehaviors
	{
		public static readonly DependencyProperty SynchronizedSelectedItems = DependencyProperty.RegisterAttached(
			"SynchronizedSelectedItems", typeof(IList), typeof(MultiSelectorBehaviors), new PropertyMetadata(null, OnSynchronizedSelectedItemsChanged));

		private static readonly DependencyProperty SynchronizationManagerProperty = DependencyProperty.RegisterAttached(
			"SynchronizationManager", typeof(SynchronizationManager), typeof(MultiSelectorBehaviors), new PropertyMetadata(null));

		/// <summary>
		/// Gets the synchronized selected items.
		/// </summary>
		/// <param name="dependencyObject">The dependency object.</param>
		/// <returns>The list that is acting as the sync list.</returns>
		public static IList GetSynchronizedSelectedItems(DependencyObject dependencyObject)
		{
			return (IList)dependencyObject.GetValue(SynchronizedSelectedItems);
		}

		/// <summary>
		/// Sets the synchronized selected items.
		/// </summary>
		/// <param name="dependencyObject">The dependency object.</param>
		/// <param name="value">The value to be set as synchronized items.</param>
		public static void SetSynchronizedSelectedItems(DependencyObject dependencyObject, IList value)
		{
			dependencyObject.SetValue(SynchronizedSelectedItems, value);
		}

		private static SynchronizationManager GetSynchronizationManager(DependencyObject dependencyObject)
		{
			return (SynchronizationManager)dependencyObject.GetValue(SynchronizationManagerProperty);
		}

		private static void SetSynchronizationManager(DependencyObject dependencyObject, SynchronizationManager value)
		{
			dependencyObject.SetValue(SynchronizationManagerProperty, value);
		}

		private static void OnSynchronizedSelectedItemsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue != null)
			{
				SynchronizationManager synchronizer = GetSynchronizationManager(dependencyObject);
				synchronizer.StopSynchronizing();

				SetSynchronizationManager(dependencyObject, null);
			}

			IList list = e.NewValue as IList;
			Selector selector = dependencyObject as Selector;

			// check that this property is an IList, and that it is being set on a ListBox
			if (list != null && selector != null)
			{
				SynchronizationManager synchronizer = GetSynchronizationManager(dependencyObject);
				if (synchronizer == null)
				{
					synchronizer = new SynchronizationManager(selector);
					SetSynchronizationManager(dependencyObject, synchronizer);
				}

				synchronizer.StartSynchronizingList();
			}
		}

		/// <summary>
		/// A synchronization manager.
		/// </summary>
		private class SynchronizationManager
		{
			private readonly Selector _multiSelector;
			private TwoListSynchronizer _synchronizer;

			/// <summary>
			/// Initializes a new instance of the <see cref="SynchronizationManager"/> class.
			/// </summary>
			/// <param name="selector">The selector.</param>
			internal SynchronizationManager(Selector selector)
			{
				_multiSelector = selector;
			}

			/// <summary>
			/// Starts synchronizing the list.
			/// </summary>
			public void StartSynchronizingList()
			{
				IList list = GetSynchronizedSelectedItems(_multiSelector);

				if (list != null)
				{
					_synchronizer = new TwoListSynchronizer(GetSelectedItemsCollection(_multiSelector), list);
					_synchronizer.StartSynchronizing();
				}
			}

			/// <summary>
			/// Stops synchronizing the list.
			/// </summary>
			public void StopSynchronizing()
			{
				_synchronizer.StopSynchronizing();
			}

			public static IList GetSelectedItemsCollection(Selector selector)
			{
				if (selector is MultiSelector)
				{
					return (selector as MultiSelector).SelectedItems;
				}
				else if (selector is ListBox)
				{
					return (selector as ListBox).SelectedItems;
				}
				else
				{
					throw new InvalidOperationException("Target object has no SelectedItems property to bind.");
				}
			}

		}
	}
}
