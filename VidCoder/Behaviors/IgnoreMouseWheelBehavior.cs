using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace VidCoder.Behaviors
{
	/// <summary>
	/// Captures and eats MouseWheel events so that a nested ListBox does not
	/// prevent an outer scrollable control from scrolling.
	/// </summary>
	public sealed class IgnoreMouseWheelBehavior : Behavior<UIElement>
	{
		protected override void OnAttached()
		{
			base.OnAttached();
			this.AssociatedObject.PreviewMouseWheel += this.AssociatedObject_PreviewMouseWheel;
		}

		protected override void OnDetaching()
		{
			this.AssociatedObject.PreviewMouseWheel -= this.AssociatedObject_PreviewMouseWheel;
			base.OnDetaching();
		}

		private void AssociatedObject_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			e.Handled = true;

			var e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
			e2.RoutedEvent = UIElement.MouseWheelEvent;

			this.AssociatedObject.RaiseEvent(e2);
		}
	}
}
