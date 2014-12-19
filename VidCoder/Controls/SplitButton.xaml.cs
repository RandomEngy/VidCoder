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
	using System.Collections.ObjectModel;
	using System.Windows.Controls.Primitives;

	/// <summary>
	/// Interaction logic for SplitButton.xaml
	/// </summary>
	public partial class SplitButton : UserControl
	{
		private Button button;

        ///// <summary>
        ///// Stores the backing collection for the ButtonMenuItemsSource property.
        ///// </summary>
        //private ObservableCollection<object> menuItemsSource = new ObservableCollection<object>();

        ///// <summary>
        ///// Gets the collection of items for the split button's menu.
        ///// </summary>
        //public Collection<object> MenuItemsSource { get { return this.menuItemsSource; } }

		public SplitButton()
		{
			this.InitializeComponent();

            this.SetValue(MenuItemsSourceProperty, new Collection<object>());
		}

	    public static readonly DependencyProperty MenuItemsSourceProperty = DependencyProperty.Register(
	        "MenuItemsSource",
	        typeof (Collection<object>),
	        typeof (SplitButton),
	        new UIPropertyMetadata(null));

	    public Collection<object> MenuItemsSource
	    {
	        get { return (Collection<object>) GetValue(MenuItemsSourceProperty); }

	        set { SetValue(MenuItemsSourceProperty, value); }
	    }

		public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
			"Command",
			typeof (ICommand),
			typeof (SplitButton),
			new UIPropertyMetadata(null, OnCommandChanged));

		public ICommand Command
		{
			get
			{
				return (ICommand) GetValue(CommandProperty);
			}

			set
			{
				SetValue(CommandProperty, value);
			}
		}

		private static void OnCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
		{
			if (eventArgs.NewValue != eventArgs.OldValue)
			{
				var splitButton = dependencyObject as SplitButton;

				if (splitButton.button != null)
				{
					splitButton.button.Command = eventArgs.NewValue as ICommand;
				}
			}
		}
		
		private void OnArrowClick(object sender, RoutedEventArgs e)
		{
			var buttonMenu = ContextMenuService.GetContextMenu(this.button);

            if (this.MenuItemsSource.Count > 0 && buttonMenu != null)
			{
				buttonMenu.IsOpen = !buttonMenu.IsOpen;
				buttonMenu.PlacementTarget = this.button;
				buttonMenu.Placement = PlacementMode.Bottom;
			}
		}

		private void SplitButton_OnLoaded(object sender, RoutedEventArgs e)
		{
			this.button = this.Template.FindName("mainButton", this) as Button;
			if (this.Command != null)
			{
				this.button.Command = this.Command;
			}
		}
	}
}
