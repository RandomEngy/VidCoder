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
using Microsoft.Practices.Unity;

namespace VidCoder.Services
{
	using Resources;

	public class Updater : IUpdater
	{
		private const string UpdateInfoUrl = "http://engy.us/VidCoder/latest.xml";
		private const string UpdateInfoUrlBeta = "http://engy.us/VidCoder/latest-beta.xml";

		public event EventHandler<EventArgs<double>> UpdateDownloadProgress;
		public event EventHandler<EventArgs> UpdateDownloadCompleted;

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

		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private BackgroundWorker updateDownloader;
		private bool processDownloadsUpdates = true;
		private bool downloading;
		private string latestInfoUrl;
		private bool downloadingBeta;

		public bool UpdateDownloading { get; set; }
		public bool UpdateReady { get; set; }
		public bool UpToDate { get; set; }

		public void PromptToApplyUpdate()
		{
			if (!DebugMode)
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
						updateConfirmation.Owner = Unity.Container.Resolve<View.MainWindow>();
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
						if (updateDownloader != null)
						{
							updateDownloader.CancelAsync();
						}
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
				installerProcess.StartInfo = new ProcessStartInfo { FileName = installerPath, Arguments = "/silent /noicons" };
				installerProcess.Start();

				// We want to make sure the program is gone when the update installer runs.
				Environment.Exit(0);
			}
		}

		public void HandleUpdatedSettings(bool updatesEnabled)
		{
			if (!DebugMode)
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
					if (updateDownloader != null)
					{
						// If we have just turned off updates, cancel any pending downloads.
						updateDownloader.CancelAsync();
					}
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

		public void CheckUpdates()
		{
			// Only check for updates in release mode.
			if (!DebugMode)
			{
				if (Utilities.CurrentProcessInstances > 1)
				{
					processDownloadsUpdates = false;

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
		}

		private void StartBackgroundUpdate()
		{
			if (this.downloading)
			{
				return;
			}

			this.downloading = true;

			bool beta = Config.BetaUpdates;

#if BETA
			beta = true;
#endif

			if (beta)
			{
				this.latestInfoUrl = UpdateInfoUrlBeta;
				this.downloadingBeta = true;
			}
			else
			{
				this.latestInfoUrl = UpdateInfoUrl;
				this.downloadingBeta = false;
			}

			this.updateDownloader = new BackgroundWorker { WorkerSupportsCancellation = true };
			this.updateDownloader.DoWork += CheckAndDownloadUpdate;
			this.updateDownloader.RunWorkerCompleted += (o, e) =>
			{
				this.UpdateDownloading = false;

				if (this.UpdateDownloadCompleted != null)
				{
					this.UpdateDownloadCompleted(this, new EventArgs());
				}
			};
			this.updateDownloader.RunWorkerAsync();
		}

		private void CheckAndDownloadUpdate(object sender, DoWorkEventArgs e)
		{
			var updateDownloader = sender as BackgroundWorker;
			SQLiteConnection connection = Database.ThreadLocalConnection;

			try
			{
				XDocument document = XDocument.Load(this.latestInfoUrl);
				XElement root = document.Root;

				string configurationElementName;
				if (IntPtr.Size == 4)
				{
					configurationElementName = "Release";
				}
				else
				{
					configurationElementName = "Release-x64";
				}

				XElement configurationElement = root.Element(configurationElementName);
				if (configurationElement == null)
				{
					return;
				}

				XElement latestElement = configurationElement.Element("Latest");
				XElement downloadElement = configurationElement.Element("DownloadLocation");
				XElement changelogLinkElement = configurationElement.Element("ChangelogLocation");

				if (latestElement == null || downloadElement == null || changelogLinkElement == null)
				{
					return;
				}

				string updateVersion = latestElement.Value;

				if (Utilities.CompareVersions(updateVersion, Utilities.CurrentVersion) > 0)
				{
					// If an update is reported to be ready but the installer doesn't exist, clear out all the
					// installer info and redownload.
					string updateInstallerLocation = Config.UpdateInstallerLocation;
					if (updateInstallerLocation != string.Empty && !File.Exists(updateInstallerLocation))
					{
						using (SQLiteTransaction transaction = connection.BeginTransaction())
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
						if (this.downloadingBeta)
						{
							updateVersionText += " Beta";
						}

						string message = string.Format(MainRes.NewVersionDownloadStartedStatus, updateVersionText);
						this.logger.Log(message);
						this.logger.ShowStatus(message);

						this.UpdateDownloading = true;

						string downloadLocation = downloadElement.Value;
						string changelogLink = changelogLinkElement.Value;
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

							while ((bytesRead = responseStream.Read(downloadBuffer, 0, downloadBuffer.Length)) > 0 && !updateDownloader.CancellationPending)
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
								using (SQLiteTransaction transaction = connection.BeginTransaction())
								{
									Config.UpdateVersion = updateVersion;
									Config.UpdateInstallerLocation = installerFilePath;
									Config.UpdateChangelogLocation = changelogLink;

									transaction.Commit();
								}

								this.UpdateReady = true;

								message = string.Format(MainRes.NewVersionDownloadFinishedStatus, updateVersionText);
								this.logger.Log(message);
								this.logger.ShowStatus(message);
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
						this.UpdateReady = true;
					}
				}
				else
				{
					this.UpToDate = true;
				}
			}
			catch (WebException)
			{
				// Fail silently
			}
			catch (System.Xml.XmlException)
			{
				// Fail silently
			}
			finally
			{
				this.downloading = false;
			}
		}
	}
}
