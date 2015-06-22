using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using System.Windows;

namespace VidCoder.Services
{
	public interface IWindowLauncher
	{
		void OpenDialog(object viewModel, object owner);
		void OpenWindow(object viewModel, object owner);
		void CloseWindow(object viewModel);
		void FocusWindow(object viewModel);
		Window GetView(object viewModel);
		void ActivateWindow(object viewModel);
	}
}
