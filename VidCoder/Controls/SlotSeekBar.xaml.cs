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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VidCoder.Controls
{
	public partial class SlotSeekBar : UserControl
	{
		public SlotSeekBar()
		{
			this.InitializeComponent();

			this.UpdateSeekBarUI();
			this.UpdateMarkers();
		}

		public static readonly DependencyProperty SlotProperty = DependencyProperty.Register(
			"Slot",
			typeof(int),
			typeof(SlotSeekBar),
			new PropertyMetadata(new PropertyChangedCallback(OnSlotChanged)));
		public int Slot
		{
			get
			{
				return (int)this.GetValue(SlotProperty);
			}

			set
			{
				this.SetValue(SlotProperty, value);
			}
		}

		public static readonly DependencyProperty NumSlotsProperty = DependencyProperty.Register(
			"NumSlots",
			typeof(int),
			typeof(SlotSeekBar),
			new PropertyMetadata(new PropertyChangedCallback(OnNumSlotsChanged)));
		public int NumSlots
		{
			get
			{
				return (int)this.GetValue(NumSlotsProperty);
			}

			set
			{
				this.SetValue(NumSlotsProperty, value);
			}
		}

		private static void OnSlotChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			if (eventArgs.NewValue != eventArgs.OldValue)
			{
				var seekBar = (SlotSeekBar)dependencyObject;

				seekBar.UpdateSeekBarUI();
			}
		}

		private static void OnNumSlotsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			if (eventArgs.NewValue != eventArgs.OldValue)
			{
				var seekBar = (SlotSeekBar)dependencyObject;

				seekBar.UpdateSeekBarUI();
				seekBar.UpdateMarkers();
			}
		}

		private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.HandleMouseEvent(e);
			this.CaptureMouse();
		}

		private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.ReleaseMouseCapture();
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				this.HandleMouseEvent(e);
			}
		}

		private void HandleMouseEvent(MouseEventArgs e)
		{
			if (!this.IsEnabled)
			{
				return;
			}

			Point location = e.GetPosition(this);

			double width = this.ActualWidth;

			if (location.X < 0 || location.X > this.ActualWidth)
			{
				return;
			}

			double slotWidth = width / (this.NumSlots - 1);

			int newSlot = (int)(location.X / slotWidth + 0.5);
			if (newSlot >= 0 && newSlot < this.NumSlots && newSlot != this.Slot)
			{
				this.Slot = newSlot;
			}
		}

		private void UpdateSeekBarUI()
		{
			if (this.NumSlots <= 0)
			{
				return;
			}

			this.filledColumn.Width = new GridLength(this.Slot, GridUnitType.Star);
			this.emptyColumn.Width = new GridLength(this.NumSlots - this.Slot - 1, GridUnitType.Star);
		}

		private void UpdateMarkers()
		{
			if (this.NumSlots <= 0)
			{
				return;
			}

			this.markersGrid.ColumnDefinitions.Clear();
			this.markersGrid.Children.Clear();

			this.markersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });

			for (int i = 0; i < this.NumSlots - 2; i++)
			{
				this.markersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
				var marker = new Polygon
				{
					Style = this.FindResource("SeekBarMarker") as Style
				};

				Grid.SetColumn(marker, i + 1);

				this.markersGrid.Children.Add(marker);
			}

			this.markersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });
		}
	}
}
