using System;
using System.IO;
using VidCoder.Model;
using Microsoft.AnyContainer;
using ReactiveUI;
using Squirrel;

namespace VidCoder.Services
{
	public class Updater : ReactiveObject, IUpdater
	{
		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private bool processDownloadsUpdates = true;

		private UpdateState state;
		public UpdateState State
		{
			get { return this.state; }
			set { this.RaiseAndSetIfChanged(ref this.state, value); }
		}

		private int updateCheckProgressPercent;
		public int UpdateCheckProgressPercent
		{
			get { return this.updateCheckProgressPercent; }
			set { this.RaiseAndSetIfChanged(ref this.updateCheckProgressPercent, value); }
		}

		public Version LatestVersion { get; set; }

		public bool PromptToApplyUpdate()
		{
			if (Utilities.SupportsUpdates)
			{
				// If updates are enabled, and we are the last process instance, prompt to apply the update.
				if (Config.UpdatesEnabled && Utilities.CurrentProcessInstances == 1)
				{
					// An update is ready, to give a prompt to apply it.
					var updateConfirmation = new ApplyUpdateConfirmation(this.LatestVersion);
					updateConfirmation.Owner = StaticResolver.Resolve<View.Main>();
					updateConfirmation.ShowDialog();

					if (updateConfirmation.Accepted)
					{
						UpdateManager.RestartApp();
						return true;
					}
				}
			}

			return false;
		}

		public void HandleUpdatedSettings(bool updatesEnabled)
		{
			if (Utilities.SupportsUpdates && updatesEnabled && this.processDownloadsUpdates)
			{
				this.StartBackgroundUpdate(isManualCheck: false);
			}
		}

		private static void DeleteUpdatesFolder()
		{
			if (Directory.Exists(Utilities.UpdatesFolder))
			{
				Directory.Delete(Utilities.UpdatesFolder, true);
			}
		}

		// Starts checking for updates
		public void CheckUpdates(bool isManualCheck)
		{
			// Only check for updates when non-portable
			if (!Utilities.SupportsUpdates)
			{
				return;
			}

			if (Utilities.CurrentProcessInstances > 1)
			{
				this.processDownloadsUpdates = false;
				return;
			}

			if (!Config.UpdatesEnabled)
			{
				return;
			}

			this.StartBackgroundUpdate(isManualCheck);
		}

		private async void StartBackgroundUpdate(bool isManualCheck)
		{
			if (this.State != UpdateState.NotStarted && this.State != UpdateState.Failed && this.State != UpdateState.UpToDate)
			{
				// Can only start updates from certain states.
				return;
			}

			try
			{
				this.UpdateCheckProgressPercent = 0;
				this.State = UpdateState.Checking;

				using var updateManager = new UpdateManager(Utilities.SquirrelUpdateUrl, Utilities.SquirrelAppId);
				ReleaseEntry releaseEntry = await updateManager.UpdateApp(progressNumber =>
				{
					this.UpdateCheckProgressPercent = progressNumber;
				});

				if (releaseEntry == null)
				{
					this.State = UpdateState.UpToDate;
				}
				else
				{
					this.LatestVersion = releaseEntry.Version.Version;
					this.State = UpdateState.UpdateReady;

					if (CustomConfig.UpdateMode == UpdateMode.PromptApplyImmediately && !isManualCheck)
					{
						DispatchUtilities.BeginInvoke(() =>
						{
							this.PromptToApplyUpdate();
						});
					}
				}
			}
			catch (Exception exception)
			{
				this.State = UpdateState.Failed;
				this.logger.Log("Update download failed." + Environment.NewLine + exception);
			}
		}
	}
}
