using Squirrel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Services;

namespace VidCoder
{
	public static class VidCoderInstall
	{
		public static void HandleSquirrelEvents()
		{
			SquirrelAwareApp.HandleEvents(onInitialInstall: OnInitialInstall, onAppUninstall: OnAppUninstall, onAppUpdate: OnAppUpdate);
		}

		/// <summary>
		/// Called when the app first installs. Will run this code and exit.
		/// </summary>
		/// <param name="version">The app version.</param>
		private static void OnInitialInstall(SemanticVersion version, IAppTools tools)
		{
			var logger = new SupportLogger("Install");
			logger.Log("Running initial install actions...");

			try
			{
				tools.CreateUninstallerRegistryEntry();
				tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

				CopyIconFilesToRoot(logger);

				RegistryUtilities.Install(logger);
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

		private static void OnAppUpdate(SemanticVersion version, IAppTools tools)
		{
			// Check if we have new reg keys. If not, we need to get rid of old ones and swap to new ones.
			// We should be able to remove this code after a while as everyone has updated far past 8.4 Beta (July 2022).
			if (!RegistryUtilities.AreRegKeysInstalled())
			{
				var logger = new SupportLogger("Update");
				try
				{
					RunElevatedSetup("uninstall", logger);
					RegistryUtilities.Install(logger);
				}
				finally
				{
					logger.Close();
				}
			}
		}

		/// <summary>
		/// Called when the app is uninstalled. Will run this code and exit.
		/// </summary>
		/// <param name="version">The app version.</param>
		private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
		{
			var logger = new SupportLogger("Uninstall");
			logger.Log("Running uninstall actions...");

			try
			{
				tools.RemoveUninstallerRegistryEntry();
				tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

				RegistryUtilities.Uninstall(logger);
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
		/// <param name="action">The action to execute. install | uninstall | activateFileWatcher | deactivateFileWatcher</param>
		/// <param name="logger">The logger to use.</param>
		public static bool RunElevatedSetup(string action, IBasicLogger logger)
		{
			logger.Log("Starting elevated setup...");

			string setupExe = Path.Combine(CommonUtilities.ProgramFolder, "VidCoderElevatedSetup.exe");
			var startInfo = new ProcessStartInfo(setupExe, action);
			startInfo.CreateNoWindow = true;
			startInfo.WorkingDirectory = CommonUtilities.ProgramFolder;
			startInfo.UseShellExecute = true;
			startInfo.Verb = "runas";

			Process process = Process.Start(startInfo);
			process.WaitForExit();

			if (process.ExitCode == 0)
			{
				logger.Log("Elevated setup completed successfully.");
				return true;
			}
			else
			{
				logger.Log("Elevated setup failed. Check the ElevatedInstall log for more details.");
				return false;
			}
		}

		/// <summary>
		/// Copies some icon files to the root local app data folder so they can be in a stable location for file associations.
		/// </summary>
		/// <param name="logger">The logger to use.</param>
		private static void CopyIconFilesToRoot(SupportLogger logger)
		{
			try
			{
				const string presetIconFileName = "VidCoderPreset.ico";
				const string queueIconFileName = "VidCoderQueue.ico";

				string presetIconSourcePath = Path.Combine(CommonUtilities.ProgramFolder, presetIconFileName);
				string queueIconSourcePath = Path.Combine(CommonUtilities.ProgramFolder, queueIconFileName);

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
