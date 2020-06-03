using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using VidCoder.Model;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.ViewModel;
using VidCoderCommon;

namespace VidCoder.Services
{
	using Resources;

	public class Updater : ReactiveObject, IUpdater
	{
		private const string UpdateInfoUrlBase = "http://engy.us/VidCoder";

		private CancellationTokenSource updateDownloadCancellationTokenSource;

		private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private bool processDownloadsUpdates = true;

		private UpdateState state;
		public UpdateState State
		{
			get { return this.state; }
			set { this.RaiseAndSetIfChanged(ref this.state, value); }
		}

		private double updateDownloadProgressFraction;
		public double UpdateDownloadProgressFraction
		{
			get { return this.updateDownloadProgressFraction; }
			set { this.RaiseAndSetIfChanged(ref this.updateDownloadProgressFraction, value); }
		}

		public Version LatestVersion { get; set; }

		public bool PromptToApplyUpdate(bool relaunchWhenComplete)
		{
			if (Utilities.SupportsUpdates)
			{
				// If updates are enabled, and we are the last process instance, prompt to apply the update.
				if (Config.UpdatesEnabled && Utilities.CurrentProcessInstances == 1)
				{
					// See if the user has already applied the update manually
					Version updateVersion;
					if (Version.TryParse(Config.UpdateVersion, out updateVersion) && updateVersion.FillInWithZeroes() <= Utilities.CurrentVersion)
					{
						// If we already have the newer version clear all the update info and cancel
						ClearUpdateMetadata();
						DeleteUpdatesFolder();
						return false;
					}

					string installerPath = Config.UpdateInstallerLocation;

					if (installerPath != string.Empty && File.Exists(installerPath))
					{
						// An update is ready, to give a prompt to apply it.
						var updateConfirmation = new ApplyUpdateConfirmation();
						updateConfirmation.Owner = StaticResolver.Resolve<View.Main>();
						updateConfirmation.ShowDialog();

						if (updateConfirmation.Result == "Yes")
						{
							this.ApplyUpdate(relaunchWhenComplete);
							return true;
						}
						else if (updateConfirmation.Result == "Disable")
						{
							Config.UpdatesEnabled = false;
						}
					}
					else
					{
						this.updateDownloadCancellationTokenSource?.Cancel();
					}
				}
			}

			return false;
		}

		public void ApplyUpdate(bool relaunchWhenComplete)
		{
			// Re-check the process count in case another one was opened while the prompt was active.
			if (Utilities.CurrentProcessInstances == 1)
			{
				string installerPath = Config.UpdateInstallerLocation;

				Config.UpdateInProgress = true;

				var installerProcess = new Process();
				string extraParameter = relaunchWhenComplete ? "/launchWhenDone=\"yes\"" : "/showSuccessDialog=\"yes\"";
				installerProcess.StartInfo = new ProcessStartInfo { FileName = installerPath, Arguments = "/silent /noicons /mergetasks=\"!desktopicon\" " + extraParameter + " /dir=\"" + Utilities.ProgramFolder + "\"" };
				installerProcess.Start();

				// Caller will handle exiting
			}
		}

		public void HandleUpdatedSettings(bool updatesEnabled)
		{
			if (Utilities.SupportsUpdates)
			{
				if (updatesEnabled)
				{
					// If we don't already have an update waiting to install, check for updates.
					if (this.processDownloadsUpdates && Config.UpdateInstallerLocation == string.Empty)
					{
						this.StartBackgroundUpdate();
					}
				}
				else
				{
					// If we have just turned off updates, cancel any pending downloads.
					this.updateDownloadCancellationTokenSource?.Cancel();
				}
			}
		}

		public bool HandlePendingUpdate()
		{
			// This flag signifies VidCoder was closed to install an update.
			// In this case we report success, delete the installer, clean up the update flags and exit.
			bool updateInProgress = Config.UpdateInProgress;
			if (updateInProgress)
			{
				Version targetUpdateVersion = Version.Parse(Config.UpdateVersion);

				// Fill in with zeroes for Build and Revision
				targetUpdateVersion = targetUpdateVersion.FillInWithZeroes();
				bool updateSucceeded = targetUpdateVersion == Utilities.CurrentVersion;

				using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
				{
					Config.UpdateInProgress = false;

					if (updateSucceeded)
					{
						ClearUpdateMetadata();
					}

					transaction.Commit();
				}

				if (updateSucceeded)
				{
					try
					{
						DeleteUpdatesFolder();
					}
					catch (IOException)
					{
						// Ignore this. Not critical that we delete the updates folder.
					}
					catch (UnauthorizedAccessException)
					{
						// Ignore this. Not critical that we delete the updates folder.
					}

					return true;
				}
				else
				{
					// If the target version is different from the currently running version,
					// this means the attempted upgrade failed. We give an error message but
					// continue with the program.
					MessageBox.Show(MainRes.UpdateNotAppliedError);

					// Need to show it twice since the first dialog is automatically dismissed if opened during splash screen.
					MessageBox.Show(MainRes.UpdateNotAppliedError);
				}
			}

			return false;
		}

		private static void DeleteUpdatesFolder()
		{
			if (Directory.Exists(Utilities.UpdatesFolder))
			{
				Directory.Delete(Utilities.UpdatesFolder, true);
			}
		}

		private static void ClearUpdateMetadata()
		{
			Config.UpdateVersion = string.Empty;
			Config.UpdateInstallerLocation = string.Empty;
			Config.UpdateChangelogLocation = string.Empty;
		}

		// Starts checking for updates
		public void CheckUpdates()
		{
			// Only check for updates when non-portable and not running under desktop bridge
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
				// On a program restart, if updates are disabled, clean any pending installers.
				Config.UpdateInstallerLocation = string.Empty;

				if (Directory.Exists(Utilities.UpdatesFolder))
				{
					Directory.Delete(Utilities.UpdatesFolder, true);
				}

				return;
			}

			this.StartBackgroundUpdate();
		}

		private async void StartBackgroundUpdate()
		{
			if (this.State != UpdateState.NotStarted && this.State != UpdateState.Failed && this.State != UpdateState.UpToDate)
			{
				// Can only start updates from certain states.
				return;
			}

			this.State = UpdateState.DownloadingInfo;

			this.updateDownloadCancellationTokenSource = new CancellationTokenSource();

			await Task.Run(async () =>
			{
				try
				{
					UpdateInfo updateInfo = await GetUpdateInfoAsync(CommonUtilities.Beta).ConfigureAwait(false);

					if (updateInfo == null)
					{
						this.State = UpdateState.Failed;
						this.logger.Log("Update download failed. Unable to get update info.");
						return;
					}

					Version updateVersion = updateInfo.LatestVersion;
					this.LatestVersion = updateVersion;

					if (updateVersion > Utilities.CurrentVersion)
					{
						// If an update is reported to be ready but the installer doesn't exist, clear out all the
						// installer info and redownload.
						string updateInstallerLocation = Config.UpdateInstallerLocation;
						if (updateInstallerLocation != string.Empty && !File.Exists(updateInstallerLocation))
						{
							using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
							{
								ClearUpdateMetadata();

								transaction.Commit();
							}

							this.logger.Log("Downloaded update (" + updateInstallerLocation + ") could not be found. Re-downloading it.");
						}

						// If we have an installer downloaded, but there's a newer one out, clear out the downloaded update metadata
						if (Config.UpdateInstallerLocation != string.Empty 
							&& Version.TryParse(Config.UpdateVersion, out Version downloadedUpdateVersion)
							&& downloadedUpdateVersion.FillInWithZeroes() < updateVersion.FillInWithZeroes())
						{
							using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
							{
								ClearUpdateMetadata();

								transaction.Commit();
							}
						}

						// If we have not finished the download update yet, start/resume the download.
						if (Config.UpdateInstallerLocation == string.Empty)
						{
							string updateVersionText = updateVersion.ToShortString();

							if (CommonUtilities.Beta)
							{
								updateVersionText += " Beta";
							}

							string newVersionStartedMessage = string.Format(MainRes.NewVersionDownloadStartedStatus, updateVersionText);
							this.logger.Log(newVersionStartedMessage);

							DispatchUtilities.BeginInvoke(() =>
							{
								this.logger.ShowStatus(newVersionStartedMessage);
							});

							this.State = UpdateState.DownloadingInstaller;
							this.UpdateDownloadProgressFraction = 0;

							string downloadLocation = updateInfo.DownloadUrl;
							string changelogLink = updateInfo.ChangelogUrl;
							string installerFileName = Path.GetFileName(downloadLocation);
							string installerFilePath = Path.Combine(Utilities.UpdatesFolder, installerFileName);

							Stream responseStream = null;
							FileStream fileStream = null;

							try
							{
								ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
								HttpClient client = new HttpClient();
								HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadLocation);

								long bytesProgressTotal = 0;

								if (File.Exists(installerFilePath))
								{
									var fileInfo = new FileInfo(installerFilePath);

									request.Headers.Range = new RangeHeaderValue(fileInfo.Length, null);
									bytesProgressTotal = fileInfo.Length;

									fileStream = new FileStream(installerFilePath, FileMode.Append, FileAccess.Write, FileShare.None);
								}
								else
								{
									fileStream = new FileStream(installerFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
								}

								var response = await client.SendAsync(request).ConfigureAwait(false);
								responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

								byte[] downloadBuffer = new byte[2048];
								int bytesRead;

								while ((bytesRead = await responseStream.ReadAsync(downloadBuffer, 0, downloadBuffer.Length).ConfigureAwait(false)) > 0 && !this.updateDownloadCancellationTokenSource.Token.IsCancellationRequested)
								{
									fileStream.Write(downloadBuffer, 0, bytesRead);
									bytesProgressTotal += bytesRead;

									this.UpdateDownloadProgressFraction = (double)bytesProgressTotal / response.Content.Headers.ContentLength.Value;
								}

								if (bytesRead == 0)
								{
									using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
									{
										Config.UpdateVersion = updateVersion.ToString();
										Config.UpdateInstallerLocation = installerFilePath;
										Config.UpdateChangelogLocation = changelogLink;

										transaction.Commit();
									}

									this.State = UpdateState.InstallerReady;
									this.UpdateDownloadProgressFraction = 1;

									string downloadFinishedFormat = CustomConfig.UpdatePromptTiming == UpdatePromptTiming.OnExit
										? MainRes.NewVersionDownloadFinishedStatus
										: MainRes.NewVersionDownloadFinishedOnLaunchStatus;
									string message = string.Format(downloadFinishedFormat, updateVersionText);
									this.logger.Log(message);
									this.logger.ShowStatus(message);
								}
								else
								{
									// In this case the download must have been canceled.
									this.State = UpdateState.NotStarted;
								}
							}
							finally
							{
								responseStream?.Close();
								fileStream?.Close();
							}
						}
						else
						{
							this.State = UpdateState.InstallerReady;
						}
					}
					else
					{
						this.State = UpdateState.UpToDate;
					}
				}
				catch (Exception exception)
				{
					this.State = UpdateState.Failed;
					this.logger.Log("Update download failed." + Environment.NewLine + exception);
				}
				finally
				{
					this.updateDownloadCancellationTokenSource?.Dispose();
					this.updateDownloadCancellationTokenSource = null;
				}

				if (this.State == UpdateState.InstallerReady && CustomConfig.UpdatePromptTiming == UpdatePromptTiming.OnLaunch)
				{
					DispatchUtilities.BeginInvoke(() =>
					{
						if (this.PromptToApplyUpdate(relaunchWhenComplete: true))
						{
							StaticResolver.Resolve<MainViewModel>().Exit.Execute(null);
						}
					});
				}
			});
		}

		internal static async Task<UpdateInfo> GetUpdateInfoAsync(bool beta)
		{
			string url = GetUpdateUrl(beta);

			try
			{
				HttpClient client = new HttpClient();
				string updateJson = await client.GetStringAsync(url);

				JsonSerializerSettings settings = new JsonSerializerSettings();
				settings.Converters.Add(new VersionConverter());

				UpdateInfo updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(updateJson, settings);
				updateInfo.LatestVersion = updateInfo.LatestVersion.FillInWithZeroes();
				return updateInfo;
			}
			catch (Exception exception)
			{
				StaticResolver.Resolve<IAppLogger>().Log("Could not get update info." + Environment.NewLine + exception);

				return null;
			}
		}

		private static string GetUpdateUrl(bool beta)
		{
			string updateInfoFile = GetUpdateFilename(beta);

			string testPortion = CommonUtilities.DebugMode ? "/Test" : string.Empty;

			return UpdateInfoUrlBase + testPortion + "/" + updateInfoFile;
		}

		private static string GetUpdateFilename(bool beta)
		{
			var fileNameBuilder = new StringBuilder("latest");
			if (beta)
			{
				fileNameBuilder.Append("-beta");
			}

			fileNameBuilder.Append(".json");

			return fileNameBuilder.ToString();
		}
	}
}
