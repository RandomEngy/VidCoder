using System;
using System.IO;
using System.Reactive;
using System.Text;
using System.Windows.Input;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class LogWindowViewModel : ReactiveObject
	{
		private MainViewModel mainViewModel = StaticResolver.Resolve<MainViewModel>();
		private IAppLogger generalLogger = StaticResolver.Resolve<IAppLogger>();

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public LogCoordinator LogCoordinator { get; } = StaticResolver.Resolve<LogCoordinator>();

		private ReactiveCommand<Unit, Unit> copy;
		public ICommand Copy
		{
			get
			{
				return this.copy ?? (this.copy = ReactiveCommand.Create(() =>
				{
					try
					{
						var selectedLogger = this.LogCoordinator.SelectedLog.Logger;
						string logContents = GetLogContents(selectedLogger);
						StaticResolver.Resolve<ClipboardService>().SetText(logContents);
					}
					catch (Exception exception)
					{
						this.generalLogger.LogError("Could not copy text." + Environment.NewLine + exception);
					}
				}));
			}
		}

		private static string GetLogContents(IAppLogger fileLogger)
		{
			lock (fileLogger.LogLock)
			{
				fileLogger.SuspendWriter();

				try
				{
					return File.ReadAllText(fileLogger.LogPath);
				}
				finally
				{
					fileLogger.ResumeWriter();
				}
			}
		}
	}
}
