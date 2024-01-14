using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Windows;
using System.Collections.Specialized;

namespace VidCoder;

/// <summary>
/// Keeps two lists synchronized. 
/// </summary>
public class TwoListSynchronizer : IWeakEventListener
{
	private static readonly IListItemConverter DefaultConverter = new DoNothingListItemConverter();
	private readonly IList _masterList;
	private readonly IListItemConverter _masterTargetConverter;
	private readonly IList _targetList;


	/// <summary>
	/// Initializes a new instance of the <see cref="TwoListSynchronizer"/> class.
	/// </summary>
	/// <param name="masterList">The master list.</param>
	/// <param name="targetList">The target list.</param>
	/// <param name="masterTargetConverter">The master-target converter.</param>
	public TwoListSynchronizer(IList masterList, IList targetList, IListItemConverter masterTargetConverter)
	{
		_masterList = masterList;
		_targetList = targetList;
		_masterTargetConverter = masterTargetConverter;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TwoListSynchronizer"/> class.
	/// </summary>
	/// <param name="masterList">The master list.</param>
	/// <param name="targetList">The target list.</param>
	public TwoListSynchronizer(IList masterList, IList targetList)
		: this(masterList, targetList, DefaultConverter)
	{
	}

	private delegate void ChangeListAction(IList list, NotifyCollectionChangedEventArgs e, Converter<object, object> converter);

	/// <summary>
	/// Starts synchronizing the lists.
	/// </summary>
	public void StartSynchronizing()
	{
		ListenForChangeEvents(_masterList);
		ListenForChangeEvents(_targetList);

		// Update the Target list from the Master list
		SetListValuesFromSource(_masterList, _targetList, ConvertFromMasterToTarget);

		// In some cases the target list might have its own view on which items should included:
		// so update the master list from the target list
		// (This is the case with a ListBox SelectedItems collection: only items from the ItemsSource can be included in SelectedItems)
		if (!TargetAndMasterCollectionsAreEqual())
		{
			SetListValuesFromSource(_targetList, _masterList, ConvertFromTargetToMaster);
		}
	}

	/// <summary>
	/// Stop synchronizing the lists.
	/// </summary>
	public void StopSynchronizing()
	{
		StopListeningForChangeEvents(_masterList);
		StopListeningForChangeEvents(_targetList);
	}

	/// <summary>
	/// Receives events from the centralized event manager.
	/// </summary>
	/// <param name="managerType">The type of the <see cref="T:System.Windows.WeakEventManager"/> calling this method.</param>
	/// <param name="sender">Object that originated the event.</param>
	/// <param name="e">Event data.</param>
	/// <returns>
	/// true if the listener handled the event. It is considered an error by the <see cref="T:System.Windows.WeakEventManager"/> handling in WPF to register a listener for an event that the listener does not handle. Regardless, the method should return false if it receives an event that it does not recognize or handle.
	/// </returns>
	public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
	{
		HandleCollectionChanged(sender as IList, e as NotifyCollectionChangedEventArgs);

		return true;
	}

	/// <summary>
	/// Listens for change events on a list.
	/// </summary>
	/// <param name="list">The list to listen to.</param>
	protected void ListenForChangeEvents(IList list)
	{
		if (list is INotifyCollectionChanged)
		{
			CollectionChangedEventManager.AddListener(list as INotifyCollectionChanged, this);
		}
	}

	/// <summary>
	/// Stops listening for change events.
	/// </summary>
	/// <param name="list">The list to stop listening to.</param>
	protected void StopListeningForChangeEvents(IList list)
	{
		if (list is INotifyCollectionChanged)
		{
			CollectionChangedEventManager.RemoveListener(list as INotifyCollectionChanged, this);
		}
	}

	private void AddItems(IList list, NotifyCollectionChangedEventArgs e, Converter<object, object> converter)
	{
		int itemCount = e.NewItems.Count;

		for (int i = 0; i < itemCount; i++)
		{
			int insertionPoint = e.NewStartingIndex + i;

			if (insertionPoint > list.Count)
			{
				list.Add(converter(e.NewItems[i]));
			}
			else
			{
				list.Insert(insertionPoint, converter(e.NewItems[i]));
			}
		}
	}

	private object ConvertFromMasterToTarget(object masterListItem)
	{
		return _masterTargetConverter == null ? masterListItem : _masterTargetConverter.Convert(masterListItem);
	}

	private object ConvertFromTargetToMaster(object targetListItem)
	{
		return _masterTargetConverter == null ? targetListItem : _masterTargetConverter.ConvertBack(targetListItem);
	}

	private void HandleCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		IList sourceList = sender as IList;

		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				PerformActionOnAllLists(AddItems, sourceList, e);
				break;
			case NotifyCollectionChangedAction.Move:
				PerformActionOnAllLists(MoveItems, sourceList, e);
				break;
			case NotifyCollectionChangedAction.Remove:
				PerformActionOnAllLists(RemoveItems, sourceList, e);
				break;
			case NotifyCollectionChangedAction.Replace:
				PerformActionOnAllLists(ReplaceItems, sourceList, e);
				break;
			case NotifyCollectionChangedAction.Reset:
				UpdateListsFromSource(sender as IList);
				break;
			default:
				break;
		}
	}

	private void MoveItems(IList list, NotifyCollectionChangedEventArgs e, Converter<object, object> converter)
	{
		RemoveItems(list, e, converter);
		AddItems(list, e, converter);
	}

	private void PerformActionOnAllLists(ChangeListAction action, IList sourceList, NotifyCollectionChangedEventArgs collectionChangedArgs)
	{
		if (sourceList == _masterList)
		{
			PerformActionOnList(_targetList, action, collectionChangedArgs, ConvertFromMasterToTarget);
		}
		else
		{
			PerformActionOnList(_masterList, action, collectionChangedArgs, ConvertFromTargetToMaster);
		}
	}

	private void PerformActionOnList(IList list, ChangeListAction action, NotifyCollectionChangedEventArgs collectionChangedArgs, Converter<object, object> converter)
	{
		StopListeningForChangeEvents(list);
		action(list, collectionChangedArgs, converter);
		ListenForChangeEvents(list);
	}

	private void RemoveItems(IList list, NotifyCollectionChangedEventArgs e, Converter<object, object> converter)
	{
		int itemCount = e.OldItems.Count;

		// for the number of items being removed, remove the item from the Old Starting Index
		// (this will cause following items to be shifted down to fill the hole).
		for (int i = 0; i < itemCount; i++)
		{
			list.RemoveAt(e.OldStartingIndex);
		}
	}

	private void ReplaceItems(IList list, NotifyCollectionChangedEventArgs e, Converter<object, object> converter)
	{
		RemoveItems(list, e, converter);
		AddItems(list, e, converter);
	}

	private void SetListValuesFromSource(IList sourceList, IList targetList, Converter<object, object> converter)
	{
		StopListeningForChangeEvents(targetList);

		targetList.Clear();

		foreach (object o in sourceList)
		{
			targetList.Add(converter(o));
		}

		ListenForChangeEvents(targetList);
	}

	private bool TargetAndMasterCollectionsAreEqual()
	{
		return _masterList.Cast<object>().SequenceEqual(_targetList.Cast<object>().Select(item => ConvertFromTargetToMaster(item)));
	}

	/// <summary>
	/// Makes sure that all synchronized lists have the same values as the source list.
	/// </summary>
	/// <param name="sourceList">The source list.</param>
	private void UpdateListsFromSource(IList sourceList)
	{
		if (sourceList == _masterList)
		{
			SetListValuesFromSource(_masterList, _targetList, ConvertFromMasterToTarget);
		}
		else
		{
			SetListValuesFromSource(_targetList, _masterList, ConvertFromTargetToMaster);
		}
	}




	/// <summary>
	/// An implementation that does nothing in the conversions.
	/// </summary>
	internal class DoNothingListItemConverter : IListItemConverter
	{
		/// <summary>
		/// Converts the specified master list item.
		/// </summary>
		/// <param name="masterListItem">The master list item.</param>
		/// <returns>The result of the conversion.</returns>
		public object Convert(object masterListItem)
		{
			return masterListItem;
		}

		/// <summary>
		/// Converts the specified target list item.
		/// </summary>
		/// <param name="targetListItem">The target list item.</param>
		/// <returns>The result of the conversion.</returns>
		public object ConvertBack(object targetListItem)
		{
			return targetListItem;
		}
	}
}
