﻿using System;
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
	/// <summary>
	/// Called when the app first installs. Will run this code and exit.
	/// </summary>
	/// <param name="version">The app version.</param>
	public static void OnInitialInstall(SemanticVersion version)
	{
		var logger = new SupportLogger("Install");
		logger.Log("Running initial install actions...");

		try
		{
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

	public static void OnAppUpdate(SemanticVersion version)
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
	public static void OnAppUninstall(SemanticVersion version)
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
}
