using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml.Linq;
using System.IO;
using System.Net;
using System.Windows;
using VidCoder.Model;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace VidCoder.Services
{
	using Resources;

	public class Updater : IUpdater
	{
		public const string UpdateInfoUrlBeta = "http://engy.us/VidCoder/latest-beta.json";
		public const string UpdateInfoUrlNonBeta = "http://engy.us/VidCoder/latest.json";

		private CancellationTokenSource updateDownloadCancellationTokenSource;

		public event EventHandler<EventArgs<double>> UpdateDownloadProgress;
		public event EventHandler<EventArgs> UpdateStateChanged;

		private static bool DebugMode
		{
			get
			{
#if DEBUG
				return true;
#else
				return false;
#endif
			}
		}

		private static bool Beta
		{
			get
			{
#if BETA
				return true;
#else
				return false;
#endif
			}
		}

		private static bool BuildSupportsUpdates
		{
			get
			{
				return !Utilities.IsPortable;
			}
		}

		private ILogger logger = Ioc.Container.GetInstance<ILogger>();
		private bool processDownloadsUpdates = true;

		private UpdateState state;
		public UpdateState State
		{
			get
			{
				return this.state;
			}

			set
			{
				if (value != this.state)
				{
					this.state = value;
					if (this.UpdateStateChanged != null)
					{
						this.UpdateStateChanged(this, new EventArgs());
					}
				}
			}
		}

		public string LatestVersion { get; set; }

		public void PromptToApplyUpdate()
		{
			if (BuildSupportsUpdates)
			{
				// If updates are enabled, and we are the last process instance, prompt to apply the update.
				if (Config.UpdatesEnabled && Utilities.CurrentProcessInstances == 1)
				{
					// See if the user has already applied the update manually
					string updateVersion = Config.UpdateVersion;
					if (!string.IsNullOrEmpty(updateVersion) && Utilities.CompareVersions(updateVersion, Utilities.CurrentVersion) <= 0)
					{
						// If we already have the newer version clear all the update info and cancel
						ClearUpdateMetadata();
						DeleteUpdatesFolder();
						return;
					}

					string installerPath = Config.UpdateInstallerLocation;

					if (installerPath != string.Empty && File.Exists(installerPath))
					{
						// An update is ready, to give a prompt to apply it.
						var updateConfirmation = new ApplyUpdateConfirmation();
						updateConfirmation.Owner = Ioc.Container.GetInstance<View.MainWindow>();
						updateConfirmation.ShowDialog();

						if (updateConfirmation.Result == "Yes")
						{
							this.ApplyUpdate();
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
		}

		public void ApplyUpdate()
		{
			// Re-check the process count in case another one was opened while the prompt was active.
			if (Utilities.CurrentProcessInstances == 1)
			{
				string installerPath = Config.UpdateInstallerLocation;

				Config.UpdateInProgress = true;

				var installerProcess = new Process();
				installerProcess.StartInfo = new ProcessStartInfo { FileName = installerPath, Arguments = "/silent /noicons /showSuccessDialog=\"yes\"" };
				installerProcess.Start();

				// Let the program close on its own.
				//Environment.Exit(0);
			}
		}

		public void HandleUpdatedSettings(bool updatesEnabled)
		{
			if (BuildSupportsUpdates)
			{
				if (updatesEnabled)
				{
					// If we don't already have an update waiting to install, check for updates.
					if (processDownloadsUpdates && Config.UpdateInstallerLocation == string.Empty)
					{
						this.StartBackgroundUpdate();
					}
				}
				else
				{
					this.updateDownloadCancellationTokenSource?.Cancel();
				}
			}
		}

		public bool HandlePendingUpdate()
		{
			// This flag signifies VidCoder is being run by the installer after an update.
			// In this case we report success, delete the installer, clean up the update flags and exit.
			bool updateInProgress = Config.UpdateInProgress;
			if (updateInProgress)
			{
				string targetUpdateVersion = Config.UpdateVersion;
				bool updateSucceeded = Utilities.CompareVersions(targetUpdateVersion, Utilities.CurrentVersion) == 0;

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
			// Only check for updates in release mode, non-portable
			if (!BuildSupportsUpdates)
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

			if (!Environment.Is64BitOperatingSystem)
			{
				if (!Config.UpdatesDisabled32BitOSWarningDisplayed)
				{
					// Need to show twice as the first one is automatically closed.
					Ioc.Container.GetInstance<IMessageBoxService>().Show(CommonRes.UpdatesDisabled32BitOSMessage);
					Ioc.Container.GetInstance<IMessageBoxService>().Show(CommonRes.UpdatesDisabled32BitOSMessage);

					Config.UpdatesDisabled32BitOSWarningDisplayed = true;
				}

				this.State = UpdateState.NotSupported32BitOS;
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

			await Task.Factory.StartNew(async () =>
			{
				try
				{
					UpdateInfo updateInfo = await GetUpdateInfoAsync(Beta);

					if (updateInfo == null)
					{
						this.State = UpdateState.Failed;
						this.logger.Log("Update download failed. Unable to get update info.");
						return;
					}

					string updateVersion = updateInfo.LatestVersion;
					this.LatestVersion = updateVersion;
					
					if (Utilities.CompareVersions(updateVersion, Utilities.CurrentVersion) > 0)
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

						// If we have not finished the download update yet, start/resume the download.
						if (Config.UpdateInstallerLocation == string.Empty)
						{
							string updateVersionText = updateVersion;
#if BETA
							updateVersionText += " Beta";
#endif

							string message = string.Format(MainRes.NewVersionDownloadStartedStatus, updateVersionText);
							this.logger.Log(message);
							this.logger.ShowStatus(message);

							this.State = UpdateState.DownloadingInstaller;

							string downloadLocation = updateInfo.DownloadUrl;
							string changelogLink = updateInfo.ChangelogUrl;
							string installerFileName = Path.GetFileName(downloadLocation);
							string installerFilePath = Path.Combine(Utilities.UpdatesFolder, installerFileName);

							Stream responseStream = null;
							FileStream fileStream = null;

							try
							{
								var request = (HttpWebRequest)WebRequest.Create(downloadLocation);
								int bytesProgressTotal = 0;

								if (File.Exists(installerFilePath))
								{
									var fileInfo = new FileInfo(installerFilePath);

									request.AddRange((int)fileInfo.Length);
									bytesProgressTotal = (int)fileInfo.Length;

									fileStream = new FileStream(installerFilePath, FileMode.Append, FileAccess.Write, FileShare.None);
								}
								else
								{
									fileStream = new FileStream(installerFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
								}

								var response = (HttpWebResponse)request.GetResponse();
								responseStream = response.GetResponseStream();

								byte[] downloadBuffer = new byte[2048];
								int bytesRead;

								while ((bytesRead = responseStream.Read(downloadBuffer, 0, downloadBuffer.Length)) > 0 && !this.updateDownloadCancellationTokenSource.Token.IsCancellationRequested)
								{
									fileStream.Write(downloadBuffer, 0, bytesRead);
									bytesProgressTotal += bytesRead;

									if (this.UpdateDownloadProgress != null)
									{
										double completionPercentage = ((double)bytesProgressTotal * 100) / response.ContentLength;
										this.UpdateDownloadProgress(this, new EventArgs<double>(completionPercentage));
									}
								}

								if (bytesRead == 0)
								{
									using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
									{
										Config.UpdateVersion = updateVersion;
										Config.UpdateInstallerLocation = installerFilePath;
										Config.UpdateChangelogLocation = changelogLink;

										transaction.Commit();
									}

									this.State = UpdateState.InstallerReady;

									message = string.Format(MainRes.NewVersionDownloadFinishedStatus, updateVersionText);
									this.logger.Log(message);
									this.logger.ShowStatus(message);
								}
								else
								{
									// In this case the download must have been cancelled.
									this.State = UpdateState.NotStarted;
								}
							}
							finally
							{
								if (responseStream != null)
								{
									responseStream.Close();
								}

								if (fileStream != null)
								{
									fileStream.Close();
								}
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
					this.logger.Log("Update download failed: " + exception.Message);
				}
			});
		}

		internal static async Task<UpdateInfo> GetUpdateInfoAsync(bool beta)
		{
			string url = beta ? UpdateInfoUrlBeta : UpdateInfoUrlNonBeta;

			try
			{
				HttpClient client = new HttpClient();
				string updateJson = await client.GetStringAsync(url);

				JsonSerializerSettings settings = new JsonSerializerSettings();
				settings.Converters.Add(new VersionConverter());

				UpdateInfo updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(updateJson, settings);
				return updateInfo;
			}
			catch (Exception exception)
			{
				Ioc.Container.GetInstance<ILogger>().Log("Could not get update info." + Environment.NewLine + exception);

				return null;
			}
		}
	}
}
