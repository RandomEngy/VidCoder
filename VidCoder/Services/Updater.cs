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
using VidCoder.Properties;
using System.Diagnostics;
using Microsoft.Practices.Unity;

namespace VidCoder.Services
{
	public class Updater : IUpdater
	{
		public const string UpdateInProgress = "UpdateInProgress";
		public const string UpdateVersion = "UpdateVersion";
		public const string UpdateInstallerLocation = "UpdateInstallerLocation";
		public const string UpdateChangelogLocation = "UpdateChangelogLocation";

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

		public bool UpdateDownloading { get; set; }
		public bool UpdateReady { get; set; }
		public bool UpToDate { get; set; }

		public void PromptToApplyUpdate()
		{
			if (!DebugMode)
			{
				// If updates are enabled, and we are the last process instance, prompt to apply the update.
				if (Settings.Default.UpdatesEnabled && Utilities.CurrentProcessInstances == 1)
				{
					string installerPath = DatabaseConfig.GetConfigString(UpdateInstallerLocation, Database.Connection);

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
							Settings.Default.UpdatesEnabled = false;
							Settings.Default.Save();
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
				string installerPath = DatabaseConfig.GetConfigString(UpdateInstallerLocation, Database.Connection);

				DatabaseConfig.SetConfigValue(UpdateInProgress, true, Database.Connection);

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
					if (processDownloadsUpdates && DatabaseConfig.GetConfigString(UpdateInstallerLocation, Database.Connection) == string.Empty)
					{
						this.StartBackgroundUpdate();
					}
				}
				else
				{
					// If we have just turned off updates, cancel any pending downloads.
					updateDownloader.CancelAsync();
				}
			}
		}

		public bool HandlePendingUpdate()
		{
			// This flag signifies VidCoder is being run by the installer after an update.
			// In this case we report success, delete the installer, clean up the update flags and exit.
			bool updateInProgress = DatabaseConfig.GetConfigBool(UpdateInProgress, Database.Connection);
			if (updateInProgress)
			{
				string targetUpdateVersion = DatabaseConfig.GetConfigString(UpdateVersion, Database.Connection);
				bool updateSucceeded = Utilities.CompareVersions(targetUpdateVersion, Utilities.CurrentVersion) == 0;

				using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
				{
					DatabaseConfig.SetConfigValue(UpdateInProgress, false, Database.Connection);

					if (updateSucceeded)
					{
						DatabaseConfig.SetConfigValue(UpdateVersion, string.Empty, Database.Connection);
						DatabaseConfig.SetConfigValue(UpdateInstallerLocation, string.Empty, Database.Connection);
						DatabaseConfig.SetConfigValue(UpdateChangelogLocation, string.Empty, Database.Connection);
					}

					transaction.Commit();
				}

				if (updateSucceeded)
				{
					try
					{
						if (Directory.Exists(Utilities.UpdatesFolder))
						{
							Directory.Delete(Utilities.UpdatesFolder, true);
						}
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
					MessageBox.Show("The update was not applied. If you did not cancel it, try installing it manually.");
				}
			}

			return false;
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

				if (!Settings.Default.UpdatesEnabled)
				{
					// On a program restart, if updates are disabled, clean any pending installers.
					DatabaseConfig.SetConfigValue(UpdateInstallerLocation, string.Empty, Database.Connection);

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

			this.UpdateDownloading = true;
		}

		private void CheckAndDownloadUpdate(object sender, DoWorkEventArgs e)
		{
			var updateDownloader = sender as BackgroundWorker;
			SQLiteConnection connection = Database.CreateConnection();

			try
			{
				XDocument document = XDocument.Load(Utilities.UpdateInfoUrl);
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
					string updateInstallerLocation = DatabaseConfig.GetConfigString(UpdateInstallerLocation, connection);
					if (updateInstallerLocation != string.Empty && !File.Exists(updateInstallerLocation))
					{
						using (SQLiteTransaction transaction = connection.BeginTransaction())
						{
							DatabaseConfig.SetConfigValue(UpdateVersion, string.Empty, Database.Connection);
							DatabaseConfig.SetConfigValue(UpdateInstallerLocation, string.Empty, Database.Connection);
							DatabaseConfig.SetConfigValue(UpdateChangelogLocation, string.Empty, Database.Connection);
							
							transaction.Commit();
						}

						this.logger.Log("Downloaded update (" + updateInstallerLocation + ") could not be found. Re-downloading it.");
					}

					// If we have not finished the download update yet, start/resume the download.
					if (DatabaseConfig.GetConfigString(UpdateInstallerLocation, connection) == string.Empty)
					{
						this.logger.Log("Started downloading update " + updateVersion);

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
									DatabaseConfig.SetConfigValue(UpdateVersion, updateVersion, connection);
									DatabaseConfig.SetConfigValue(UpdateInstallerLocation, installerFilePath, connection);
									DatabaseConfig.SetConfigValue(UpdateChangelogLocation, changelogLink, connection);
									
									transaction.Commit();
								}

								this.UpdateReady = true;

								this.logger.Log("Update " + updateVersion + " has finished downloading and will install on exit.");
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
		}
	}
}
