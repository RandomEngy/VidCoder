using System;
using System.Linq;
using System.Reactive;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;

namespace VidCoder.Extensions;

    public static class WindowExtensions
    {
    private static IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();

        public static void RegisterGlobalHotkeys(this Window window)
        {
		var converter = new KeyConverter();
		foreach (var definition in WindowManager.Definitions.Where(d => d.InputGestureText != null))
        {
	        if (definition.InputGestureText.Contains("+"))
	        {
		        string[] parts = definition.InputGestureText.Split('+');
		        if (parts.Length == 2)
		        {
			        Type windowViewModelType = definition.ViewModelType;

			        var key = (Key)converter.ConvertFrom(parts[1]);

			        ReactiveCommand<Unit, Unit> openCommand = ReactiveCommand.Create(() =>
			        {
						windowManager.OpenOrFocusWindow(windowViewModelType);
					});

					window.InputBindings.Add(new InputBinding(
						openCommand, 
						new KeyGesture(key, ModifierKeys.Control)));
		        }
		        else
		        {
			        throw new ArgumentException("InputGestureText not recognized: " + definition.InputGestureText);
		        }
	        }
        }

            window.InputBindings.Add(new InputBinding(windowManager.CreateOpenCommand(typeof(OptionsDialogViewModel), openAsDialog: true), new KeyGesture(Key.G, ModifierKeys.Control)));
        window.InputBindings.Add(new InputBinding(StaticResolver.Resolve<ProcessingService>().Encode, new KeyGesture(Key.F5)));
        }

	public static void SetPlacementJson(this Window window, string placementJson)
	{
		WindowPlacement.SetPlacement(new WindowInteropHelper(window).Handle, placementJson);
	}

	public static string GetPlacementJson(this Window window)
	{
		return WindowPlacement.GetPlacement(new WindowInteropHelper(window).Handle);
	}

    public static bool PlaceDynamic(this Window window, string placementJson)
    {
		if (string.IsNullOrEmpty(placementJson))
		{
			Rect? placementRect = new WindowPlacer().PlaceWindow(window);
			if (placementRect.HasValue)
			{
				window.Left = placementRect.Value.Left;
				window.Top = placementRect.Value.Top;

				return true;
			}

			return false;
		}
		else
		{
			window.SetPlacementJson(placementJson);
			return false;
		}
    }
    }
