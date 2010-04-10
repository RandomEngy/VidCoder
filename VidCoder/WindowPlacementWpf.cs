using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace VidCoder
{
    public static class WindowPlacementWpf
    {
        public static void SetPlacement(this Window window, string placementXml)
        {
            WindowPlacement.SetPlacement(new WindowInteropHelper(window).Handle, placementXml);
        }

        public static string GetPlacement(this Window window)
        {
            return WindowPlacement.GetPlacement(new WindowInteropHelper(window).Handle);
        }
    }
}
