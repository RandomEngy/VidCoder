using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.ViewModel;
using VidCoder.Services;
using System.Windows;

namespace VidCoder
{
	public static class WindowManager
	{
		private static List<ViewModelBase> openWindows = new List<ViewModelBase>();
		private static List<ViewModelBase> openDialogs = new List<ViewModelBase>();

		private static IWindowLauncher windowLauncher = new WindowLauncher();

		internal static IWindowLauncher WindowLauncher
		{
			get
			{
				return windowLauncher;
			}

			set
			{
				windowLauncher = value;
			}
		}

		public static List<ViewModelBase> OpenWindows
		{
			get
			{
				return openWindows;
			}
		}

		public static void OpenWindow(ViewModelBase viewModel)
		{
			OpenWindow(viewModel, null);
		}

		public static void OpenDialog(ViewModelBase viewModel)
		{
			OpenDialog(viewModel, null);
		}

		public static void OpenWindow(ViewModelBase viewModel, ViewModelBase owner)
		{
			openWindows.Add(viewModel);
			WindowLauncher.OpenWindow(viewModel, owner);
		}

		public static void OpenDialog(ViewModelBase viewModel, ViewModelBase owner)
		{
			openDialogs.Add(viewModel);
			WindowLauncher.OpenDialog(viewModel, owner);
		}

		public static void Close(ViewModelBase viewModel)
		{
			if (openWindows.Contains(viewModel))
			{
				openWindows.Remove(viewModel);
			}
			else if (openDialogs.Contains(viewModel))
			{
				openDialogs.Remove(viewModel);
			}

			WindowLauncher.CloseWindow(viewModel);
		}

		public static void ReportClosed(ViewModelBase viewModel)
		{
			if (openWindows.Contains(viewModel))
			{
				openWindows.Remove(viewModel);
			}
			else if (openDialogs.Contains(viewModel))
			{
				openDialogs.Remove(viewModel);
			}
		}

		public static void FocusWindow(ViewModelBase viewModel)
		{
			windowLauncher.FocusWindow(viewModel);
		}

		public static ViewModelBase FindWindow(Type viewModelType)
		{
			return openWindows.SingleOrDefault(vm => vm.GetType() == viewModelType);
		}

		internal static Window GetView(ViewModelBase viewModel)
		{
			return windowLauncher.GetView(viewModel);
		}
	}
}
