using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using VidCoder.Services;

namespace VidCoder.Extensions
{
    public static class WindowExtensions
    {
        private static WindowManagerService windowManager = Ioc.Container.GetInstance<WindowManagerService>();

        public static void RegisterGlobalHotkeys(this Window window)
        {
            window.InputBindings.Add(new InputBinding(windowManager.OpenEncodingWindowCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenPreviewWindowCommand, new KeyGesture(Key.P, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenPickerWindowCommand, new KeyGesture(Key.I, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenLogWindowCommand, new KeyGesture(Key.L, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenOptionsCommand, new KeyGesture(Key.F4)));
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
}
