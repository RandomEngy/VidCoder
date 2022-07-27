using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class WatcherWindowViewModel : ReactiveObject
	{
		private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();
		private WatcherProcessManager watcherProcessManager = StaticResolver.Resolve<WatcherProcessManager>();
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

		public WatcherWindowViewModel()
		{
			// WindowTitle
			this.WhenAnyValue(x => x.watcherProcessManager.Status)
				.Select(status =>
				{
					return string.Format(WatcherRes.WatcherWindowTitle, GetStatusString(status));
				})
				.ToProperty(this, x => x.WindowTitle, out this.windowTitle);
		}

		private ObservableAsPropertyHelper<string> windowTitle;
		public string WindowTitle => this.windowTitle.Value;

		private bool watcherEnabled = Config.WatcherEnabled;
		public bool WatcherEnabled
		{
			get { return this.watcherEnabled; }
			set
			{
				Config.WatcherEnabled = value;
				this.RaiseAndSetIfChanged(ref this.watcherEnabled, value);
				if (value)
				{
					this.watcherProcessManager.Start();
				}
				else
				{
					this.watcherProcessManager.Stop();
				}
			}
		}

		private bool runWhenClosed = RegistryUtilities.IsFileWatcherAutoStart();
		public bool RunWhenClosed
		{
			get { return this.runWhenClosed; }
			set
			{
				this.RaiseAndSetIfChanged(ref this.runWhenClosed, value);
				if (value)
				{
					RegistryUtilities.SetFileWatcherAutoStart(this.logger);
				}
				else
				{
					RegistryUtilities.RemoveFileWatcherAutoStart(this.logger);
				}
			}
		}

		private ReactiveCommand<Unit, Unit> openConfig;
		public ICommand OpenConfig
		{
			get
			{
				return this.openConfig ?? (this.openConfig = ReactiveCommand.Create(
					() =>
					{
						var watcherConfigDialog = new WatcherConfigDialogViewModel();
						this.windowManager.OpenDialog(watcherConfigDialog, this);

						if (watcherConfigDialog.DialogResult)
						{
							WatcherStorage.SaveWatchedFolders(Database.Connection, watcherConfigDialog.WatchedFolders.Items.Select(watchedFolderViewModel => watchedFolderViewModel.WatchedFolder).ToList());
						}
					}));
			}
		}

		private static string GetStatusString(WatcherProcessStatus status)
		{
			switch (status)
			{
				case WatcherProcessStatus.Running:
					return EnumsRes.WatcherProcessStatus_Running;
				case WatcherProcessStatus.Starting:
					return EnumsRes.WatcherProcessStatus_Starting;
				case WatcherProcessStatus.Stopped:
					return EnumsRes.WatcherProcessStatus_Stopped;
				case WatcherProcessStatus.Disabled:
					return EnumsRes.WatcherProcessStatus_Disabled;
				default:
					return string.Empty;
			}
		}
	}
}
