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
		void OpenDialog(ViewModelBase viewModel, ViewModelBase owner);
		void OpenWindow(ViewModelBase viewModel, ViewModelBase owner);
		void CloseWindow(ViewModelBase viewModel);
		void FocusWindow(ViewModelBase viewModel);
		Window GetView(ViewModelBase viewModel);
	}
}
