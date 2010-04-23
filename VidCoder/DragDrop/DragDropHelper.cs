using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections;
using System.Windows.Documents;
using System.Reflection;
using System.Diagnostics;

namespace VidCoder.DragDropUtils
{
	public class DragDropHelper
	{
		// source and target
		private DataFormat format = DataFormats.GetDataFormat("DragDropItemsControl");
		private Point initialMousePosition;
		private object draggedData;
		private DraggedAdorner draggedAdorner;
		private InsertionAdorner insertionAdorner;
		private Window topWindow;
		// source
		private ItemsControl sourceItemsControl;
		private FrameworkElement sourceItemContainer;
		// target
		private ItemsControl targetItemsControl;
		private FrameworkElement targetItemContainer;
		private bool hasVerticalOrientation;
		private int insertionIndex;
		private bool isInFirstHalf;
		// singleton
		private static DragDropHelper instance;
		private static DragDropHelper Instance 
		{
			get 
			{  
				if(instance == null)
				{
					instance = new DragDropHelper();
				}
				return instance;
			}
		}

		public static bool GetIsDragSource(DependencyObject obj)
		{
			return (bool)obj.GetValue(IsDragSourceProperty);
		}

		public static void SetIsDragSource(DependencyObject obj, bool value)
		{
			obj.SetValue(IsDragSourceProperty, value);
		}

		public static readonly DependencyProperty IsDragSourceProperty =
			DependencyProperty.RegisterAttached("IsDragSource", typeof(bool), typeof(DragDropHelper), new UIPropertyMetadata(false, IsDragSourceChanged));


		public static bool GetIsDropTarget(DependencyObject obj)
		{
			return (bool)obj.GetValue(IsDropTargetProperty);
		}

		public static void SetIsDropTarget(DependencyObject obj, bool value)
		{
			obj.SetValue(IsDropTargetProperty, value);
		}

		public static readonly DependencyProperty IsDropTargetProperty =
			DependencyProperty.RegisterAttached("IsDropTarget", typeof(bool), typeof(DragDropHelper), new UIPropertyMetadata(false, IsDropTargetChanged));

		public static DataTemplate GetDragDropTemplate(DependencyObject obj)
		{
			return (DataTemplate)obj.GetValue(DragDropTemplateProperty);
		}

		public static void SetDragDropTemplate(DependencyObject obj, DataTemplate value)
		{
			obj.SetValue(DragDropTemplateProperty, value);
		}

		public static readonly DependencyProperty DragDropTemplateProperty =
			DependencyProperty.RegisterAttached("DragDropTemplate", typeof(DataTemplate), typeof(DragDropHelper), new UIPropertyMetadata(null));

        public static bool GetAllowDropAtTop(DependencyObject obj)
        {
            return (bool)obj.GetValue(AllowDropAtTopProperty);
        }

        public static void SetAllowDropAtTop(DependencyObject obj, DataTemplate value)
        {
            obj.SetValue(AllowDropAtTopProperty, value);
        }

        public static readonly DependencyProperty AllowDropAtTopProperty =
            DependencyProperty.RegisterAttached("AllowDropAtTop", typeof(bool), typeof(DragDropHelper), new UIPropertyMetadata(true));

		private static void IsDragSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
		{
			var dragSource = obj as ItemsControl;
			if (dragSource != null)
			{
				if (Object.Equals(e.NewValue, true))
				{
					dragSource.PreviewMouseLeftButtonDown += Instance.DragSource_PreviewMouseLeftButtonDown;
					dragSource.PreviewMouseLeftButtonUp += Instance.DragSource_PreviewMouseLeftButtonUp;
					dragSource.PreviewMouseMove += Instance.DragSource_PreviewMouseMove;
				}
				else
				{
					dragSource.PreviewMouseLeftButtonDown -= Instance.DragSource_PreviewMouseLeftButtonDown;
					dragSource.PreviewMouseLeftButtonUp -= Instance.DragSource_PreviewMouseLeftButtonUp;
					dragSource.PreviewMouseMove -= Instance.DragSource_PreviewMouseMove;
				}
			}
		}

		private static void IsDropTargetChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
		{
			var dropTarget = obj as ItemsControl;
			if (dropTarget != null)
			{
				if (Object.Equals(e.NewValue, true))
				{
					dropTarget.AllowDrop = true;
					dropTarget.PreviewDrop += Instance.DropTarget_PreviewDrop;
					dropTarget.PreviewDragEnter += Instance.DropTarget_PreviewDragEnter;
					dropTarget.PreviewDragOver += Instance.DropTarget_PreviewDragOver;
					dropTarget.PreviewDragLeave += Instance.DropTarget_PreviewDragLeave;
				}
				else
				{
					dropTarget.AllowDrop = false;
					dropTarget.PreviewDrop -= Instance.DropTarget_PreviewDrop;
					dropTarget.PreviewDragEnter -= Instance.DropTarget_PreviewDragEnter;
					dropTarget.PreviewDragOver -= Instance.DropTarget_PreviewDragOver;
					dropTarget.PreviewDragLeave -= Instance.DropTarget_PreviewDragLeave;
				}
			}
		}

		// DragSource

		private void DragSource_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.sourceItemsControl = (ItemsControl)sender;
			Visual visual = e.OriginalSource as Visual;

			this.topWindow = (Window)Utilities.FindAncestor(typeof(Window), this.sourceItemsControl);			
			this.initialMousePosition = e.GetPosition(this.topWindow);

			this.sourceItemContainer = Utilities.GetItemContainer(this.sourceItemsControl, visual);
			if (this.sourceItemContainer != null)
			{
                IDragItem dragData = this.sourceItemContainer.DataContext as IDragItem;
                if (dragData == null || dragData.CanDrag)
                {
                    this.draggedData = this.sourceItemContainer.DataContext;
                }
			}
		}

		// Drag = mouse down + move by a certain amount
		private void DragSource_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (this.draggedData != null)
			{
				// Only drag when user moved the mouse by a reasonable amount.
				if (Utilities.IsMovementBigEnough(this.initialMousePosition, e.GetPosition(this.topWindow)))
				{
					DataObject data = new DataObject(this.format.Name, this.draggedData);

					// Adding events to the window to make sure dragged adorner comes up when mouse is not over a drop target.
					bool previousAllowDrop = this.topWindow.AllowDrop;
					this.topWindow.AllowDrop = true;
					this.topWindow.DragEnter += TopWindow_DragEnter;
					this.topWindow.DragOver += TopWindow_DragOver;
					this.topWindow.DragLeave += TopWindow_DragLeave;
					
					DragDropEffects effects = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);

					// Without this call, there would be a bug in the following scenario: Click on a data item, and drag
					// the mouse very fast outside of the window. When doing this really fast, for some reason I don't get 
					// the Window leave event, and the dragged adorner is left behind.
					// With this call, the dragged adorner will disappear when we release the mouse outside of the window,
					// which is when the DoDragDrop synchronous method returns.
					RemoveDraggedAdorner();

					this.topWindow.AllowDrop = previousAllowDrop;
					this.topWindow.DragEnter -= TopWindow_DragEnter;
					this.topWindow.DragOver -= TopWindow_DragOver;
					this.topWindow.DragLeave -= TopWindow_DragLeave;
					
					this.draggedData = null;
				}
			}
		}
			
		private void DragSource_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.draggedData = null;
		}

		// DropTarget

		private void DropTarget_PreviewDragEnter(object sender, DragEventArgs e)
		{
			this.targetItemsControl = (ItemsControl)sender;
			object draggedItem = e.Data.GetData(this.format.Name);

			DecideDropTarget(e);
			if (draggedItem != null)
			{
				// Dragged Adorner is created on the first enter only.
				ShowDraggedAdorner(e.GetPosition(this.topWindow));
				CreateInsertionAdorner();
			}
			e.Handled = true;
		}

		private void DropTarget_PreviewDragOver(object sender, DragEventArgs e)
		{
			object draggedItem = e.Data.GetData(this.format.Name);

			DecideDropTarget(e);
			if (draggedItem != null)
			{
				// Dragged Adorner is only updated here - it has already been created in DragEnter.
				ShowDraggedAdorner(e.GetPosition(this.topWindow));
				UpdateInsertionAdornerPosition();
			}
			e.Handled = true;
		}

		private void DropTarget_PreviewDrop(object sender, DragEventArgs e)
		{
			object draggedItem = e.Data.GetData(this.format.Name);
			int indexRemoved = -1;

			if (draggedItem != null)
			{
				if ((e.Effects & DragDropEffects.Move) != 0)
				{
					indexRemoved = Utilities.RemoveItemFromItemsControl(this.sourceItemsControl, draggedItem);
				}
				// This happens when we drag an item to a later position within the same ItemsControl.
				if (indexRemoved != -1 && this.sourceItemsControl == this.targetItemsControl && indexRemoved < this.insertionIndex)
				{
					this.insertionIndex--;
				}
				Utilities.InsertItemInItemsControl(this.targetItemsControl, draggedItem, this.insertionIndex);

				RemoveDraggedAdorner();
				RemoveInsertionAdorner();
			}
			e.Handled = true;
		}

		private void DropTarget_PreviewDragLeave(object sender, DragEventArgs e)
		{
			// Dragged Adorner is only created once on DragEnter + every time we enter the window. 
			// It's only removed once on the DragDrop, and every time we leave the window. (so no need to remove it here)
			object draggedItem = e.Data.GetData(this.format.Name);

			if (draggedItem != null)
			{
				RemoveInsertionAdorner();
			}
			e.Handled = true;
		}

		// If the types of the dragged data and ItemsControl's source are compatible, 
		// there are 3 situations to have into account when deciding the drop target:
		// 1. mouse is over an items container
		// 2. mouse is over the empty part of an ItemsControl, but ItemsControl is not empty
		// 3. mouse is over an empty ItemsControl.
		// The goal of this method is to decide on the values of the following properties: 
		// targetItemContainer, insertionIndex and isInFirstHalf.
		private void DecideDropTarget(DragEventArgs e)
		{
			int targetItemsControlCount = this.targetItemsControl.Items.Count;
			object draggedItem = e.Data.GetData(this.format.Name);
            this.targetItemContainer = null;

			if (IsDropDataTypeAllowed(draggedItem))
			{
				if (targetItemsControlCount > 0)
				{
					this.hasVerticalOrientation = Utilities.HasVerticalOrientation(this.targetItemsControl.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement);

                    // Hack, only works with vertical orientation lists. Original code assumed dragging to end of list if no
                    //  item is hovered over; this is incorrect.
                    for (int i = 0; i < targetItemsControlCount; i++)
                    {
                        FrameworkElement currentItemContainer = this.targetItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                        Point relativeDistanceFromItem = e.GetPosition(currentItemContainer);

                        if (relativeDistanceFromItem.Y < 0)
                        {
                            this.targetItemContainer = currentItemContainer;
                            break;
                        }

                        if (relativeDistanceFromItem.Y < currentItemContainer.ActualHeight)
                        {
                            this.targetItemContainer = currentItemContainer;
                            break;
                        }
                    }

					if (this.targetItemContainer != null)
					{
                        Point positionRelativeToItemContainer = e.GetPosition(this.targetItemContainer);
						this.isInFirstHalf = Utilities.IsInFirstHalf(this.targetItemContainer, positionRelativeToItemContainer, this.hasVerticalOrientation);
						this.insertionIndex = this.targetItemsControl.ItemContainerGenerator.IndexFromContainer(this.targetItemContainer);

						if (!this.isInFirstHalf)
						{
                            this.insertionIndex++;
						}
					}
					else
					{
                        this.targetItemContainer = this.targetItemsControl.ItemContainerGenerator.ContainerFromIndex(targetItemsControlCount - 1) as FrameworkElement;
						this.isInFirstHalf = false;
						this.insertionIndex = targetItemsControlCount;
					}

                    if (this.insertionIndex == 0 && !GetAllowDropAtTop(this.sourceItemsControl))
                    {
                        this.targetItemContainer = null;
                        this.insertionIndex = -1;
                        e.Effects = DragDropEffects.None;
                    }
				}
				else
				{
					this.insertionIndex = 0;
				}
			}
			else
			{
				this.insertionIndex = -1;
				e.Effects = DragDropEffects.None;
			}
		}

		// Can the dragged data be added to the destination collection?
		// It can if destination is bound to IList<allowed type>, IList or not data bound.
		private bool IsDropDataTypeAllowed(object draggedItem)
		{
			bool isDropDataTypeAllowed;
			IEnumerable collectionSource = this.targetItemsControl.ItemsSource;
			if (draggedItem != null)
			{
				if (collectionSource != null)
				{
					Type draggedType = draggedItem.GetType();
					Type collectionType = collectionSource.GetType();

					Type genericIListType = collectionType.GetInterface("IList`1");
					if (genericIListType != null)
					{
						Type[] genericArguments = genericIListType.GetGenericArguments();
						isDropDataTypeAllowed = genericArguments[0].IsAssignableFrom(draggedType);
					}
					else if (typeof(IList).IsAssignableFrom(collectionType))
					{
						isDropDataTypeAllowed = true;
					}
					else
					{
						isDropDataTypeAllowed = false;
					}
				}
				else // the ItemsControl's ItemsSource is not data bound.
				{
					isDropDataTypeAllowed = true;
				}
			}
			else
			{
				isDropDataTypeAllowed = false;			
			}
			return isDropDataTypeAllowed;
		}

		// Window

		private void TopWindow_DragEnter(object sender, DragEventArgs e)
		{
			ShowDraggedAdorner(e.GetPosition(this.topWindow));
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void TopWindow_DragOver(object sender, DragEventArgs e)
		{
			ShowDraggedAdorner(e.GetPosition(this.topWindow));
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void TopWindow_DragLeave(object sender, DragEventArgs e)
		{
			RemoveDraggedAdorner();
			e.Handled = true;
		}

		// Adorners

		// Creates or updates the dragged Adorner. 
		private void ShowDraggedAdorner(Point currentPosition)
		{
			if (this.draggedAdorner == null)
			{
				var adornerLayer = AdornerLayer.GetAdornerLayer(this.sourceItemsControl);
				this.draggedAdorner = new DraggedAdorner(this.draggedData, GetDragDropTemplate(this.sourceItemsControl), this.sourceItemContainer, adornerLayer);
			}
			this.draggedAdorner.SetPosition(currentPosition.X - this.initialMousePosition.X, currentPosition.Y - this.initialMousePosition.Y);
		}

		private void RemoveDraggedAdorner()
		{
			if (this.draggedAdorner != null)
			{
				this.draggedAdorner.Detach();
				this.draggedAdorner = null;
			}
		}

		private void CreateInsertionAdorner()
		{
			if (this.targetItemContainer != null)
			{
                // Here, I need to get adorner layer from targetItemContainer and not targetItemsControl. 
				// This way I get the AdornerLayer within ScrollContentPresenter, and not the one under AdornerDecorator (Snoop is awesome).
                // If I used targetItemsControl, the adorner would hang out of ItemsControl when there's a horizontal scroll bar.
				var adornerLayer = AdornerLayer.GetAdornerLayer(this.targetItemContainer);
				this.insertionAdorner = new InsertionAdorner(this.hasVerticalOrientation, this.isInFirstHalf, this.targetItemContainer, adornerLayer);
			}
		}

		private void UpdateInsertionAdornerPosition()
		{
			if (this.insertionAdorner != null)
			{
				this.insertionAdorner.IsInFirstHalf = this.isInFirstHalf;
				this.insertionAdorner.InvalidateVisual();
			}
		}

		private void RemoveInsertionAdorner()
		{
			if (this.insertionAdorner != null)
			{
				this.insertionAdorner.Detach();
				this.insertionAdorner = null;
			}
		}
	}
}
