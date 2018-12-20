using System;
using System.Reactive;
using System.Text;
using System.Windows.Input;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class LogWindowViewModel : ReactiveObject
	{
		private MainViewModel mainViewModel = StaticResolver.Resolve<MainViewModel>();
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		private ReactiveCommand<Unit, Unit> clearLog;
		public ICommand ClearLog
		{
			get
			{
				return this.clearLog ?? (this.clearLog = ReactiveCommand.Create(() =>
				{
					lock (this.logger.LogLock)
					{
						this.logger.ClearLog();
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> copy;
		public ICommand Copy
		{
			get
			{
				return this.copy ?? (this.copy = ReactiveCommand.Create(() =>
				{
					lock (this.logger.LogLock)
					{
						var logTextBuilder = new StringBuilder();

						foreach (LogEntry entry in this.logger.LogEntries)
						{
							logTextBuilder.AppendLine(entry.Text);
						}

						StaticResolver.Resolve<ClipboardService>().SetText(logTextBuilder.ToString());
					}
				}));
			}
		}
	}
}
