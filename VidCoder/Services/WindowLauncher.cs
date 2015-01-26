using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using GalaSoft.MvvmLight;
using VidCoder.View;
using VidCoder.ViewModel;
using System.ComponentModel;

namespace VidCoder.Services
{
	public class WindowLauncher : IWindowLauncher
	{
		private Dictionary<ViewModelBase, Window> openWindows = new Dictionary<ViewModelBase, Window>();

		public void OpenDialog(ViewModelBase viewModel, ViewModelBase owner)
		{
			this.PrepareWindow(viewModel, owner).ShowDialog();
		}

		public void OpenWindow(ViewModelBase viewModel, ViewModelBase owner)
		{
			this.PrepareWindow(viewModel, owner).Show();
		}

		// This bypasses the normal call to vm.OnClosing(). Closes the window without any prompting.
		public void CloseWindow(ViewModelBase viewModel)
		{
			this.openWindows[viewModel].Closing -= this.OnClosing;
			this.openWindows[viewModel].Close();
		}

		public void FocusWindow(ViewModelBase viewModel)
		{
			if (this.openWindows.ContainsKey(viewModel))
			{
				this.openWindows[viewModel].Focus();
			}
		}

		public Window GetView(ViewModelBase viewModel)
		{
			if (this.openWindows.ContainsKey(viewModel))
			{
				return this.openWindows[viewModel];
			}

			return null;
		}

		private Window PrepareWindow(ViewModelBase viewModel, ViewModelBase owner)
		{
			Window windowToOpen = null;

			if (viewModel is MainViewModel)
			{
				windowToOpen = new MainWindow();
			}
			else if (viewModel is EncodingViewModel)
			{
				windowToOpen = new EncodingWindow();
			}
			else if (viewModel is SubtitleDialogViewModel)
			{
				windowToOpen = new SubtitleDialog();
			}
			else if (viewModel is ChooseNameViewModel)
			{
				windowToOpen = new ChooseName();
			}
			else if (viewModel is OptionsDialogViewModel)
			{
				windowToOpen = new OptionsDialog();
			}
			else if (viewModel is ChapterMarkersDialogViewModel)
			{
				windowToOpen = new ChapterMarkersDialog();
			}
			else if (viewModel is PreviewViewModel)
			{
				windowToOpen = new PreviewWindow();
			}
			else if (viewModel is AboutDialogViewModel)
			{
				windowToOpen = new AboutDialog();
			}
			else if (viewModel is LogViewModel)
			{
				windowToOpen = new LogWindow();
			}
			else if (viewModel is QueueColumnsViewModel)
			{
				windowToOpen = new QueueColumnsDialog();
			}
			else if (viewModel is ScanMultipleDialogViewModel)
			{
				windowToOpen = new ScanMultipleDialog();
			}
			else if (viewModel is QueueTitlesWindowViewModel)
			{
				windowToOpen = new QueueTitlesWindow();
			}
			else if (viewModel is AddAutoPauseProcessDialogViewModel)
			{
				windowToOpen = new AddAutoPauseProcessDialog();
			}
			else if (viewModel is FileConflictDialogViewModel)
			{
				windowToOpen = new FileConflictDialog();
			}
			else if (viewModel is EncodeDetailsViewModel)
			{
				windowToOpen = new EncodeDetailsWindow();
			}
			else if (viewModel is ShutdownWarningViewModel)
			{
				windowToOpen = new ShutdownWarningWindow();
            }
            else if (viewModel is PickerWindowViewModel)
            {
                windowToOpen = new PickerWindow();
            }

			if (owner != null)
			{
				windowToOpen.Owner = openWindows[owner];
			}

			windowToOpen.DataContext = viewModel;
			windowToOpen.Closing += this.OnClosing;

			this.openWindows.Add(viewModel, windowToOpen);
			return windowToOpen;
		}

		// Fires when the user closes a window.
		private void OnClosing(object sender, CancelEventArgs e)
		{
			var closingWindow = sender as Window;

			if (closingWindow.DataContext is IDialogViewModel)
			{
				var dialogVM = closingWindow.DataContext as IDialogViewModel;
				dialogVM.OnClosing();
			}
			else
			{
				WindowManager.ReportClosed(closingWindow.DataContext as ViewModelBase);
			}

			if (closingWindow.Owner != null)
			{
				closingWindow.Owner.Activate();
			}
		}
	}
}
