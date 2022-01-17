using Squirrel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;
using VidCoderCommon.Services;

namespace VidCoder
{
	public static class VidCoderInstall
	{
		public static void HandleSquirrelEvents()
		{
			SquirrelAwareApp.HandleEvents(onInitialInstall: OnInitialInstall, onAppUninstall: OnAppUninstall);
		}

		/// <summary>
		/// Called when the app first installs. Will run this code and exit.
		/// </summary>
		/// <param name="version">The app version.</param>
		private static void OnInitialInstall(Version version)
		{
			var logger = new ElevatedSetupLogger("Install");
			logger.Log("Running initial install actions...");

			try
			{
				using var mgr = new UpdateManager(Utilities.SquirrelUpdateUrl, Utilities.SquirrelAppId);
				mgr.CreateUninstallerRegistryEntry();
				mgr.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

				CopyIconFilesToRoot(logger);

				RunElevatedSetup(install: true, logger);
				logger.Log("Initial install actions complete.");
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
				throw;
			}
			finally
			{
				logger.Close();
			}
		}

		/// <summary>
		/// Called when the app is uninstalled. Will run this code and exit.
		/// </summary>
		/// <param name="version">The app version.</param>
		private static void OnAppUninstall(Version version)
		{
			var logger = new ElevatedSetupLogger("Uninstall");
			logger.Log("Running uninstall actions...");

			try
			{
				using var mgr = new UpdateManager(Utilities.SquirrelUpdateUrl, Utilities.SquirrelAppId);
				mgr.RemoveUninstallerRegistryEntry();
				mgr.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

				RunElevatedSetup(install: false, logger);
				logger.Log("Uninstall actions complete.");
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
				throw;
			}
			finally
			{
				logger.Close();
			}
		}

		/// <summary>
		/// Runs portions of setup that need admin privilege escalation (e.g. registry edits).
		/// </summary>
		/// <param name="install">True if we are installing, false if we are uninstalling.</param>
		/// <param name="logger">The logger to use.</param>
		private static void RunElevatedSetup(bool install, ElevatedSetupLogger logger)
		{
			logger.Log("Starting elevated setup...");

			string setupExe = Path.Combine(Utilities.ProgramFolder, "VidCoderElevatedSetup.exe");
			var startInfo = new ProcessStartInfo(setupExe, install ? "install" : "uninstall");
			startInfo.CreateNoWindow = true;
			startInfo.WorkingDirectory = Utilities.ProgramFolder;
			startInfo.UseShellExecute = true;
			startInfo.Verb = "runas";

			Process process = Process.Start(startInfo);
			process.WaitForExit();

			if (process.ExitCode == 0)
			{
				logger.Log("Elevated setup completed successfully.");
			}
			else
			{
				logger.Log("Elevated setup failed. Check the ElevatedInstall log for more details.");
			}
		}

		/// <summary>
		/// Copies some icon files to the root local app data folder so they can be in a stable location for file associations.
		/// </summary>
		/// <param name="logger">The logger to use.</param>
		private static void CopyIconFilesToRoot(ElevatedSetupLogger logger)
		{
			try
			{
				const string presetIconFileName = "VidCoderPreset.ico";
				const string queueIconFileName = "VidCoderQueue.ico";

				string presetIconSourcePath = Path.Combine(Utilities.ProgramFolder, presetIconFileName);
				string queueIconSourcePath = Path.Combine(Utilities.ProgramFolder, queueIconFileName);

				string presetIconDestinationPath = Path.Combine(CommonUtilities.LocalAppFolder, presetIconFileName);
				string queueIconDestinationPath = Path.Combine(CommonUtilities.LocalAppFolder, queueIconFileName);

				File.Copy(presetIconSourcePath, presetIconDestinationPath, overwrite: true);
				File.Copy(queueIconSourcePath, queueIconDestinationPath, overwrite: true);
			}
			catch (Exception exception)
			{
				logger.Log(exception.ToString());
			}
		}
	}
}
