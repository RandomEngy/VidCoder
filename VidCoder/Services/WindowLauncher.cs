using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
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

        public void CloseWindow(ViewModelBase viewModel)
        {
            openWindows[viewModel].Closing -= this.OnClosing;
            openWindows[viewModel].Close();
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
            else if (viewModel is ChoosePresetNameViewModel)
            {
                windowToOpen = new ChoosePresetName();
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

            if (owner != null)
            {
                windowToOpen.Owner = openWindows[owner];
            }

            windowToOpen.DataContext = viewModel;
            windowToOpen.Closing += this.OnClosing;

            this.openWindows.Add(viewModel, windowToOpen);
            return windowToOpen;
        }

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
