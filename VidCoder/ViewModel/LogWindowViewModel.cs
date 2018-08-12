using System;
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
		private MainViewModel mainViewModel = Resolver.Resolve<MainViewModel>();
		private IAppLogger logger = Resolver.Resolve<IAppLogger>();

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		private ReactiveCommand clearLog;
		public ReactiveCommand ClearLog
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

		private ReactiveCommand copy;
		public ReactiveCommand Copy
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

						Resolver.Resolve<ClipboardService>().SetText(logTextBuilder.ToString());
					}
				}));
			}
		}
	}
}
