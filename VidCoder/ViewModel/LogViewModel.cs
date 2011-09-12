using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Practices.Unity;
using VidCoder.Model;
using VidCoder.Services;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	public class LogViewModel : OkCancelDialogViewModel
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private WindowManagerViewModel windowManagerVM = Unity.Container.Resolve<WindowManagerViewModel>();
		private ILogger logger = Unity.Container.Resolve<ILogger>();

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
					this.clearLogCommand = new RelayCommand(param =>
					{
						this.logger.ClearLog();
						this.NotifyPropertyChanged("LogText");
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
					this.copyCommand = new RelayCommand(param =>
					{
						lock (this.logger.LogLock)
						{
							var logTextBuilder = new StringBuilder();

							foreach (LogEntry entry in this.logger.LogEntries)
							{
								logTextBuilder.AppendLine(entry.Text);
							}

							Clipboard.SetText(logTextBuilder.ToString());
						}
					});
				}

				return this.copyCommand;
			}
		}
	}
}
