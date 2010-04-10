using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml.Linq;
using System.IO;
using System.Net;
using System.Windows;
using VidCoder.Properties;
using System.Diagnostics;

namespace VidCoder.Services
{
    public class UpdateService : IUpdateService
    {
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
                    string installerPath = Settings.Default.UpdateInstallerLocation;

                    if (installerPath != string.Empty)
                    {
                        // An update is ready, to give a prompt to apply it.
                        ApplyUpdateConfirmation updateConfirmation = new ApplyUpdateConfirmation();
                        updateConfirmation.Owner = VidCoder.View.MainWindow.TheWindow;
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
                string installerPath = Settings.Default.UpdateInstallerLocation;

                Settings.Default.UpdateInProgress = true;
                Settings.Default.Save();

                Process installerProcess = new Process();
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
                    if (processDownloadsUpdates && Settings.Default.UpdateInstallerLocation == string.Empty)
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

        public void HandlePendingUpdate()
        {
            bool updateInProgress = Settings.Default.UpdateInProgress;
            string targetUpdateVersion = Settings.Default.UpdateVersion;

            if (updateInProgress)
            {
                Settings.Default.UpdateInProgress = false;
                Settings.Default.Save();

                // This flag signifies VidCoder is being run by the installer after an update.
                // In this case we just report success and exit.
                if (Utilities.CompareVersions(targetUpdateVersion, Utilities.CurrentVersion) == 0)
                {
                    MessageBox.Show("VidCoder has been successfully updated.");
                    Environment.Exit(0);
                }

                // If the target version is different from the currently running version,
                // this means the attempted upgrade failed. We give an error message but
                // continue with the program.
                MessageBox.Show("The update was not applied. If you did not cancel it, try installing it manually.");
            }
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
                    Settings.Default.UpdateInstallerLocation = string.Empty;
                    Settings.Default.Save();

                    if (Directory.Exists(Utilities.UpdatesFolder))
                    {
                        Directory.Delete(Utilities.UpdatesFolder, true);
                    }

                    return;
                }

                string targetUpdateVersion = Settings.Default.UpdateVersion;

                if (targetUpdateVersion != string.Empty && Utilities.CompareVersions(targetUpdateVersion, Utilities.CurrentVersion) <= 0)
                {
                    // We've installed the update, so clean up the installer and reset the update flags.
                    if (Directory.Exists(Utilities.UpdatesFolder))
                    {
                        Directory.Delete(Utilities.UpdatesFolder, true);
                    }

                    Settings.Default.UpdateVersion = string.Empty;
                    Settings.Default.UpdateInstallerLocation = string.Empty;
                    Settings.Default.UpdateChangelogLocation = string.Empty;
                    Settings.Default.Save();
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
            BackgroundWorker updateDownloader = sender as BackgroundWorker;

            try
            {
                XDocument document = XDocument.Load(Utilities.UpdateInfoUrl);
                XElement root = document.Root;

                XElement releaseElement = root.Element("Release");
                if (releaseElement == null)
                {
                    return;
                }

                XElement latestElement = releaseElement.Element("Latest");
                XElement downloadElement = releaseElement.Element("DownloadLocation");
                XElement changelogLinkElement = releaseElement.Element("ChangelogLocation");

                if (latestElement == null || downloadElement == null || changelogLinkElement == null)
                {
                    return;
                }

                string updateVersion = latestElement.Value;

                if (Utilities.CompareVersions(updateVersion, Utilities.CurrentVersion) > 0)
                {
                    // If we have not finished the download update yet, start/resume the download.
                    if (Settings.Default.UpdateInstallerLocation == string.Empty)
                    {
                        string downloadLocation = downloadElement.Value;
                        string changelogLink = changelogLinkElement.Value;
                        string installerFileName = Path.GetFileName(downloadLocation);
                        string installerFilePath = Path.Combine(Utilities.UpdatesFolder, installerFileName);

                        Stream responseStream = null;
                        FileStream fileStream = null;

                        try
                        {
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(downloadLocation);
                            int bytesProgressTotal = 0;

                            if (File.Exists(installerFilePath))
                            {
                                FileInfo fileInfo = new FileInfo(installerFilePath);

                                request.AddRange((int)fileInfo.Length);
                                bytesProgressTotal = (int)fileInfo.Length;

                                fileStream = new FileStream(installerFilePath, FileMode.Append, FileAccess.Write, FileShare.None);
                            }
                            else
                            {
                                fileStream = new FileStream(installerFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                            }

                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                            responseStream = response.GetResponseStream();

                            byte[] downloadBuffer = new byte[2048];
                            int bytesRead = 0;

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
                                Settings.Default.UpdateVersion = updateVersion;
                                Settings.Default.UpdateInstallerLocation = installerFilePath;
                                Settings.Default.UpdateChangelogLocation = changelogLink;
                                Settings.Default.Save();

                                this.UpdateReady = true;
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
