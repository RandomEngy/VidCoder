using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.Threading;

namespace VidCoderCLI
{
	public class Program
	{
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

			switch (action)
			{
				case "encode":
					Encode(ReadArguments(args));
					break;
				case "scan":
					Scan(ReadArguments(args));
					break;
				case "importpreset":
					ImportPreset(args);
					break;
				case "importqueue":
					ImportQueue(args);
					break;
				default:
					Console.WriteLine("Action not recognized.");
					PrintUsage();
					break;
			}
		}

		private static void Encode(Dictionary<string, string> argumentDict)
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

			RunAction(a => a.Encode(source, destination, preset, picker), "Encode started.", "Could not start encode.");
		}

		private static void Scan(Dictionary<string, string> argumentDict)
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
						Console.WriteLine("Argument not recognized.");
						PrintUsage();
						return;
				}
			}

			if (string.IsNullOrWhiteSpace(source))
			{
				Console.WriteLine("Source is missing.");
				PrintUsage();
				return;
			}

			RunAction(a => a.Scan(source), "Scan started.", "Could not start scan.");
		}

		private static void ImportPreset(string[] args)
		{
			if (args.Length != 2)
			{
				PrintUsage();
				return;
			}

			string filePath = args[1];

			// Further checks on the file path will happen inside the app
			RunAction(a => a.ImportPreset(filePath), "Preset imported.", "Failed to import preset.");
		}

		private static void ImportQueue(string[] args)
		{
			if (args.Length != 2)
			{
				PrintUsage();
				return;
			}

			string filePath = args[1];

			// Further checks on the file path will happen inside the app
			RunAction(a => a.ImportQueue(filePath), "Queue imported.", "Failed to import queue.");
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

		private static void RunAction(Action<IVidCoderAutomation> action, string startedText, string failedText)
		{
			var firstAttemptResult = TryAction(action);
			if (firstAttemptResult == AutomationResult.Success)
			{
				Console.WriteLine(startedText);
				return;
			}

			if (firstAttemptResult == AutomationResult.FailedInVidCoder)
			{
				return;
			}

			string vidCoderExe = GetVidCoderExePath();

			if (!VidCoderIsRunning(vidCoderExe))
			{
				Console.WriteLine("Could not find a running instance of VidCoder. Starting it now.");
				Process.Start(vidCoderExe);
			}

			for (int i = 0; i < 30; i++)
			{
				Thread.Sleep(1000);
				var encodeResult = TryAction(action);

				if (encodeResult == AutomationResult.Success)
				{
					Console.WriteLine(startedText);
					return;
				}

				if (encodeResult == AutomationResult.FailedInVidCoder)
				{
					return;
				}
			}

			Console.WriteLine(failedText);
		}

		private static AutomationResult TryAction(Action<IVidCoderAutomation> action)
		{
			try
			{
				var binding = new NetNamedPipeBinding
				{
					OpenTimeout = TimeSpan.FromSeconds(30),
					CloseTimeout = TimeSpan.FromSeconds(30),
					SendTimeout = TimeSpan.FromSeconds(30),
					ReceiveTimeout = TimeSpan.FromSeconds(30)
				};

				string betaString = string.Empty;
#if BETA
				betaString = "Beta";
#endif

				var pipeFactory = new ChannelFactory<IVidCoderAutomation>(
					binding,
					new EndpointAddress("net.pipe://localhost/VidCoderAutomation" + betaString));

				IVidCoderAutomation channel = pipeFactory.CreateChannel();
				action(channel);

				return AutomationResult.Success;
			}
			catch (FaultException<AutomationError> exception)
			{
				Console.WriteLine(exception.Detail.Message);
				return AutomationResult.FailedInVidCoder;
			}
			catch (CommunicationException)
			{
				return AutomationResult.ConnectionFailed;
			}
			catch (TimeoutException)
			{
				return AutomationResult.ConnectionFailed;
			}
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
