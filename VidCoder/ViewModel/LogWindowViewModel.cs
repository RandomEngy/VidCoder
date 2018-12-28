using System;
using System.IO;
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

		public LogCoordinator LogCoordinator { get; } = StaticResolver.Resolve<LogCoordinator>();

		private ReactiveCommand<Unit, Unit> copy;
		public ICommand Copy
		{
			get
			{
				return this.copy ?? (this.copy = ReactiveCommand.Create(() =>
				{
					var selectedLogger = this.LogCoordinator.SelectedLog.Logger;

					lock (selectedLogger.LogLock)
					{
						selectedLogger.SuspendWriter();

						try
						{
							string logText = File.ReadAllText(selectedLogger.LogPath);
							StaticResolver.Resolve<ClipboardService>().SetText(logText);
						}
						finally
						{
							selectedLogger.ResumeWriter();
						}
					}
				}));
			}
		}
	}
}
