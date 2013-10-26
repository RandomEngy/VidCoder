using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using VidCoder.Model;
using VidCoder.Services;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	public class LogViewModel : OkCancelDialogViewModel
	{
		private MainViewModel mainViewModel = Ioc.Container.GetInstance<MainViewModel>();
		private WindowManagerViewModel windowManagerVM = Ioc.Container.GetInstance<WindowManagerViewModel>();
		private ILogger logger = Ioc.Container.GetInstance<ILogger>();

		private ICommand clearLogCommand;
		private ICommand copyCommand;

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public WindowManagerViewModel WindowManagerVM
		{
			get
			{
				return this.windowManagerVM;
			}
		}

		public ICommand ClearLogCommand
		{
			get
			{
				if (this.clearLogCommand == null)
				{
					this.clearLogCommand = new RelayCommand(() =>
					{
						this.logger.ClearLog();
					});
				}

				return this.clearLogCommand;
			}
		}

		public ICommand CopyCommand
		{
			get
			{
				if (this.copyCommand == null)
				{
					this.copyCommand = new RelayCommand(() =>
					{
						lock (this.logger.LogLock)
						{
							var logTextBuilder = new StringBuilder();

							foreach (LogEntry entry in this.logger.LogEntries)
							{
								logTextBuilder.AppendLine(entry.Text);
							}

							Ioc.Container.GetInstance<ClipboardService>().SetText(logTextBuilder.ToString());
						}
					});
				}

				return this.copyCommand;
			}
		}
	}
}
