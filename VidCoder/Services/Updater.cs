using System;
using System.IO;
using VidCoder.Model;
using Microsoft.AnyContainer;
using ReactiveUI;
using Velopack;

namespace VidCoder.Services;

public class Updater : ReactiveObject, IUpdater
{
	private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

	private UpdateState state = UpdateState.NotStarted;
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
			// If updates are enabled prompt to apply the update.
			if (Config.UpdatesEnabled)
			{
				// An update is ready, to give a prompt to apply it.
				var updateConfirmation = new ApplyUpdateConfirmation(this.LatestVersion);
				updateConfirmation.Owner = StaticResolver.Resolve<View.Main>();
				updateConfirmation.ShowDialog();

				if (updateConfirmation.Accepted)
				{
					var updateManager = new UpdateManager(Utilities.VelopackUpdateUrl);
					updateManager.ApplyUpdatesAndRestart();

					return true;
				}
			}
		}

		return false;
	}

	public void HandleUpdatedSettings(bool updatesEnabled)
	{
		if (Utilities.SupportsUpdates && updatesEnabled)
		{
			this.StartBackgroundUpdate(isManualCheck: false);
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

		if (!Config.UpdatesEnabled && !isManualCheck)
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
			// TODO: Removed UpdateCheckProgressPercent if we don't find where it's exposed in Velopack
			this.UpdateCheckProgressPercent = 0;
			this.State = UpdateState.Checking;

			var updateManager = new UpdateManager(Utilities.VelopackUpdateUrl);
			Velopack.UpdateInfo updateInfo = await updateManager.CheckForUpdatesAsync();

			if (updateInfo == null)
			{
				this.State = UpdateState.UpToDate;
			}
			else
			{
				// Save latest version (just major and minor)
				this.LatestVersion = new Version(updateInfo.TargetFullRelease.Version.Major, updateInfo.TargetFullRelease.Version.Minor);
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
