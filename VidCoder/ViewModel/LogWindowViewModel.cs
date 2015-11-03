using System;
using System.Text;
using System.Windows.Input;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class LogWindowViewModel : ReactiveObject
	{
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
		private ILogger logger = Ioc.Get<ILogger>();

		public LogWindowViewModel()
		{
			this.ClearLog = ReactiveCommand.Create();
			this.ClearLog.Subscribe(_ => this.ClearLogImpl());

			this.Copy = ReactiveCommand.Create();
			this.Copy.Subscribe(_ => this.CopyImpl());
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public ReactiveCommand<object> ClearLog { get; }
		private void ClearLogImpl()
		{
			this.logger.ClearLog();
		}

		public ReactiveCommand<object> Copy { get; }
		private void CopyImpl()
		{
			lock (this.logger.LogLock)
			{
				var logTextBuilder = new StringBuilder();

				foreach (LogEntry entry in this.logger.LogEntries)
				{
					logTextBuilder.AppendLine(entry.Text);
				}

				Ioc.Get<ClipboardService>().SetText(logTextBuilder.ToString());
			}
		}
	}
}
