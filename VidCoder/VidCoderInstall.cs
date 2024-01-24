using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Versioning;
using Velopack;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Services;

namespace VidCoder;

public static class VidCoderInstall
{
	public static void SetUpVelopack()
	{
		VelopackApp.Build()
			.WithFirstRun(OnInitialInstall)
			.WithAfterUpdateFastCallback(OnAppUpdate)
			.WithBeforeUninstallFastCallback(OnAppUninstall)
			.Run();
	}

	/// <summary>
	/// Called when the app first installs. Will run this code and exit.
	/// </summary>
	/// <param name="version">The app version.</param>
	private static void OnInitialInstall(SemanticVersion version)
	{
		var logger = new SupportLogger("Install");
		logger.Log("Running initial install actions...");

		try
		{
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

	private static void OnAppUpdate(SemanticVersion version)
	{
		// Check if we have new reg keys. If not, we need to get rid of old ones and swap to new ones.
		// We should be able to remove this code after a while as everyone has updated far past 8.4 Beta (July 2022).
		if (!RegistryUtilities.AreRegKeysInstalled())
		{
			var logger = new SupportLogger("Update");
			try
			{
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
	private static void OnAppUninstall(SemanticVersion version)
	{
		var logger = new SupportLogger("Uninstall");
		logger.Log("Running uninstall actions...");

		try
		{
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
