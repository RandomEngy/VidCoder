using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using PipeMethodCalls;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Services;

namespace VidCoderCLI
{
	public class Program
	{
		private static string cli_timespan;
		private static string source;
		private static string destination;
		private static string preset;
		private static string picker;

		public static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				PrintUsage();
				return;
			}

			Console.WriteLine("args: " + string.Join(" ", args));

			string action = args[0].ToLowerInvariant();
			Console.WriteLine("Action: '" + action + "'");
			Console.WriteLine("Action length: " + action.Length);

			RunActionStringAsync(action, args).Wait();
		}

		private static Task RunActionStringAsync(string action, string[] args)
		{
			switch (action)
			{
				case "encode":
					return EncodeAsync(ReadArguments(args));
				case "scan":
					return ScanAsync(ReadArguments(args));
				case "importpreset":
					return ImportPresetAsync(args);
				case "importqueue":
					return ImportQueueAsync(args);
				default:
					WriteError("Action not recognized.");
					PrintUsage();
					return Task.CompletedTask;
			}
		}

		private static async Task EncodeAsync(Dictionary<string, string> argumentDict)
		{
			foreach (string token in argumentDict.Keys)
			{
				switch (token)
				{
					case "s":
					case "source":
						source = argumentDict[token];
						break;
					case "d":
					case "destination":
						destination = argumentDict[token];
						break;
					case "p":
					case "preset":
						preset = argumentDict[token];
						break;
					case "picker":
						picker = argumentDict[token];
						break;
					case "timespan":
						cli_timespan = argumentDict[token];
						break;
					default:
						PrintUsage();
						return;
				}
			}

			if (string.IsNullOrWhiteSpace(source))
			{
				PrintUsage();
				return;
			}

			if (destination == string.Empty)
			{
				PrintUsage();
				return;
			}

			if (string.IsNullOrWhiteSpace(preset))
			{
				PrintUsage();
				return;
			}

			if (picker == string.Empty)
			{
				PrintUsage();
				return;
			}

			await SetupAndRunActionAsync(a => a.Encode(cli_timespan, source, destination, preset, picker), "Encode started.", "Could not start encode.").ConfigureAwait(false);
		}

		private static async Task ScanAsync(Dictionary<string, string> argumentDict)
		{
			foreach (string token in argumentDict.Keys)
			{
				switch (token)
				{
					case "s":
					case "source":
						source = argumentDict[token];
						break;
					default:
						WriteError("Argument not recognized.");
						PrintUsage();
						return;
				}
			}

			if (string.IsNullOrWhiteSpace(source))
			{
				WriteError("Source is missing.");
				PrintUsage();
				return;
			}

			await SetupAndRunActionAsync(a => a.Scan(source), "Scan started.", "Could not start scan.").ConfigureAwait(false);
		}

		private static async Task ImportPresetAsync(string[] args)
		{
			if (args.Length != 2)
			{
				PrintUsage();
				return;
			}

			string filePath = args[1];

			// Further checks on the file path will happen inside the app
			await SetupAndRunActionAsync(a => a.ImportPreset(filePath), "Preset imported.", "Failed to import preset.").ConfigureAwait(false);
		}

		private static async Task ImportQueueAsync(string[] args)
		{
			if (args.Length != 2)
			{
				PrintUsage();
				return;
			}

			string filePath = args[1];

			// Further checks on the file path will happen inside the app
			await SetupAndRunActionAsync(a => a.ImportQueue(filePath), "Queue imported.", "Failed to import queue.").ConfigureAwait(false);
		}

		private static async Task SetupAndRunActionAsync(Expression<Action<IVidCoderAutomation>> action, string startedText, string failedText)
		{
			string vidCoderExe = GetVidCoderExePath();

			if (!VidCoderIsRunning(vidCoderExe))
			{
				Console.WriteLine("Could not find a running instance of VidCoder. Starting it now.");
				Process.Start(vidCoderExe);
				await Task.Delay(1000).ConfigureAwait(false);
			}

			var client = new AutomationClient();
			try
			{
				AutomationResult result = await client.RunActionAsync(action);
				switch (result)
				{
					case AutomationResult.Success:
						Console.WriteLine(startedText);
						break;
					case AutomationResult.ConnectionFailed:
						WriteError(failedText);
						break;
					default:
						break;
				}
			}
			catch (Exception exception)
			{
				WriteError(exception.ToString());
			}
		}

		private static Dictionary<string, string> ReadArguments(string[] args)
		{
			var result = new Dictionary<string, string>();

			for (int i = 1; i < args.Length; i++)
			{
				if (args[i].StartsWith("-"))
				{
					var token = args[i].Substring(1).ToLowerInvariant();
					i++;

					if (i >= args.Length)
					{
						return result;
					}

					result.Add(token, args[i]);
				}
				else
				{
					return result;
				}
			}

			return result;
		}

		private static void PrintUsage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("VidCoderCLI encode > optional use 'timespan' encode > -timespan \"0:00:10-0:22:01\"");
			Console.WriteLine("VidCoderCLI encode -s[ource] \"<source path>\" [-d[estination] \"<encode file destination>\"] -p[reset] \"<preset name>\" [-picker \"<picker name>\"]");
			Console.WriteLine("VidCoderCLI scan -s[ource] \"<source path>\"");
			Console.WriteLine("VidCoderCLI importpreset \"<preset file path>\"");
			Console.WriteLine("VidCoderCLI importqueue \"<queue file path>\"");
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

		private static void WriteError(string error)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(error);
			Console.ResetColor();
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
