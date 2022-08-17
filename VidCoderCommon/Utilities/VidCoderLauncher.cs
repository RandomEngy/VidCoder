using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Services;

namespace VidCoderCommon.Utilities
{
	public static class VidCoderLauncher
	{
		public static async Task SetupAndRunActionAsync(Expression<Action<IVidCoderAutomation>> action)
		{
			if (LaunchVidCoderIfNotRunning())
			{
				await Task.Delay(1000).ConfigureAwait(false);
			}

			await RunActionAsync(action).ConfigureAwait(false);
		}

		public static async Task RunActionAsync(Expression<Action<IVidCoderAutomation>> action)
		{
			var client = new AutomationClient();
			AutomationResult result = await client.RunActionAsync(action).ConfigureAwait(false);
			switch (result)
			{
				case AutomationResult.ConnectionFailed:
					throw new Exception("Connection failed.");
				case AutomationResult.FailedInVidCoder:
					throw new Exception("Operation failed in VidCoder.");
				default:
					break;
			}
		}

		/// <summary>
		/// Launches VidCoder if it isn't running.
		/// </summary>
		/// <returns>True if we had to launch VidCoder.</returns>
		public static bool LaunchVidCoderIfNotRunning()
		{
			string vidCoderExe = GetVidCoderExePath();

			if (VidCoderIsRunning(vidCoderExe))
			{
				return false;
			}

			Console.WriteLine("Could not find a running instance of VidCoder. Starting it now.");
			Process.Start(vidCoderExe);
			return true;
		}

		public static bool VidCoderIsRunning()
		{
			return VidCoderIsRunning(GetVidCoderExePath());
		}

		private static bool VidCoderIsRunning(string vidCoderExe)
		{
			var processes1 = Process.GetProcessesByName("VidCoder");
			var processes2 = Process.GetProcessesByName("VidCoder.vshost");

			var processes = new List<Process>();
			processes.AddRange(processes1);
			processes.AddRange(processes2);

			foreach (var process in processes)
			{
				if (process.Modules != null && process.Modules.Count > 0 && process.Modules[0].FileName != null)
				{
					if (string.Compare(process.Modules[0].FileName, vidCoderExe, StringComparison.InvariantCultureIgnoreCase) == 0)
					{
						return true;
					}
				}
			}

			return false;
		}

		private static string GetVidCoderExePath()
		{
			string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string currentDirectory = Path.GetDirectoryName(currentExePath);

			string vidCoderExeCandidate = Path.Combine(currentDirectory, "VidCoder.exe");

			if (File.Exists(vidCoderExeCandidate))
			{
				return vidCoderExeCandidate;
			}

			return null;
		}
	}
}
