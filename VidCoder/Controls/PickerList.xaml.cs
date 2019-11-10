using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VidCoder.DragDropUtils;

namespace VidCoder.Controls
{
	/// <summary>
	/// Interaction logic for PickerList.xaml
	/// </summary>
	public partial class PickerList : UserControl
	{
		public PickerList()
		{
			this.InitializeComponent();
		}

		public event MouseEventHandler ItemMouseUp;

		private void OnItemMouseUp(object sender, MouseButtonEventArgs e)
		{
			this.ItemMouseUp?.Invoke(this, e);
		}

		public const string EnableDragDropPropertyName = "EnableDragDrop";
		public static readonly DependencyProperty EnableDragDropProperty = DependencyProperty.Register(
			EnableDragDropPropertyName,
			typeof(bool),
			typeof(PickerList),
			new UIPropertyMetadata(false, new PropertyChangedCallback(OnEnableDragDropPropertyChanged)));
		public bool EnableDragDrop
		{
			get
			{
				return (bool)GetValue(EnableDragDropProperty);
			}
			set
			{
				SetValue(EnableDragDropProperty, value);
			}
		}

		private static void OnEnableDragDropPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			var newValue = (bool)eventArgs.NewValue;
			if (newValue)
			{
				var pickerList = (PickerList)dependencyObject;
				DragDropHelper.SetIsDragSource(pickerList.listBox, true);
				DragDropHelper.SetIsDropTarget(pickerList.listBox, true);
			}
		}
	}
}
