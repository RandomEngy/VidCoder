using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using VidCoder.ViewModel.Components;

namespace VidCoder.Extensions
{
    public static class WindowExtensions
    {
        private static WindowManagerViewModel windowManager = Ioc.Container.GetInstance<WindowManagerViewModel>();

        public static void RegisterGlobalHotkeys(this Window window)
        {
            window.InputBindings.Add(new InputBinding(windowManager.OpenEncodingWindowCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenPreviewWindowCommand, new KeyGesture(Key.P, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenPickerWindowCommand, new KeyGesture(Key.I, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenLogWindowCommand, new KeyGesture(Key.L, ModifierKeys.Control)));
            window.InputBindings.Add(new InputBinding(windowManager.OpenOptionsCommand, new KeyGesture(Key.F4)));
        }
    }
}
