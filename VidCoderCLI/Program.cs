using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCLI
{
	using System.Diagnostics;
	using System.IO;
	using System.ServiceModel;
	using System.Threading;

	public class Program
	{
		private static string source;
		private static string destination;
		private static string preset;

		public static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				PrintUsage();
				return;
			}

			if (args[0].ToLowerInvariant() != "encode")
			{
				PrintUsage();
				return;
			}

			Dictionary<string, string> argumentDict = ReadArguments(args);
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

			var firstAttemptResult = TryEncode(source, destination, preset);
			if (firstAttemptResult == AutomationEncodeResult.Success)
			{
				Console.WriteLine("Encode started.");
				return;
			}

			if (firstAttemptResult == AutomationEncodeResult.FailedInVidCoder)
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
				var encodeResult = TryEncode(source, destination, preset);
				if (encodeResult == AutomationEncodeResult.Success)
				{
					Console.WriteLine("Encode started.");
					return;
				}

				if (encodeResult == AutomationEncodeResult.FailedInVidCoder)
				{
					return;
				}
			}

			Console.WriteLine("Could not start encode.");
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
			Console.WriteLine("Usage: VidCoderCLI encode -s[ource] \"<source path>\" [-d[estination] \"<encode file destination>\"] -p[reset] \"<preset name>\"");
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

		private static AutomationEncodeResult TryEncode(string source, string destination, string presetName)
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

				channel.Encode(source, destination, presetName);
				return AutomationEncodeResult.Success;
			}
			catch (FaultException<AutomationError> exception)
			{
				Console.WriteLine(exception.Detail.Message);
				return AutomationEncodeResult.FailedInVidCoder;
			}
			catch (CommunicationException)
			{
				return AutomationEncodeResult.ConnectionFailed;
			}
			catch (TimeoutException)
			{
				return AutomationEncodeResult.ConnectionFailed;
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
