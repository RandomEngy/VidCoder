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
using VidCoderCommon.Utilities;

namespace VidCoderCLI;

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

		RunActionStringAsync(action, args).Wait();
	}

	private static async Task RunActionStringAsync(string action, string[] args)
	{
		try
		{
			switch (action)
			{
				case "encode":
					await EncodeAsync(ReadArguments(args));
					break;
				case "scan":
					await ScanAsync(ReadArguments(args));
					break;
				case "importpreset":
					await ImportPresetAsync(args);
					break;
				case "importqueue":
					await ImportQueueAsync(args);
					break;
				case "pause":
					await PauseAsync();
					break;
				case "resume":
					await ResumeAsync();
					break;
				default:
					WriteError("Action not recognized.");
					PrintUsage();
					break;
			}
		}
		catch (Exception exception)
		{
			WriteError(exception.ToString());
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

		await VidCoderLauncher.SetupAndRunActionAsync(a => a.Encode(source, destination, preset, picker)).ConfigureAwait(false);
		Console.WriteLine("Encode started.");
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

		await VidCoderLauncher.SetupAndRunActionAsync(a => a.Scan(source)).ConfigureAwait(false);
		Console.WriteLine("Scan started.");
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
		await VidCoderLauncher.SetupAndRunActionAsync(a => a.ImportPreset(filePath)).ConfigureAwait(false);
		Console.WriteLine("Preset imported.");
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
		await VidCoderLauncher.SetupAndRunActionAsync(a => a.ImportQueue(filePath)).ConfigureAwait(false);
		Console.WriteLine("Queue imported.");
	}

	private static async Task PauseAsync()
	{
		await VidCoderLauncher.RunActionIfVidCoderIsRunning(a => a.Pause()).ConfigureAwait(false);
	}

	private static async Task ResumeAsync()
	{
		await VidCoderLauncher.RunActionIfVidCoderIsRunning(a => a.Resume()).ConfigureAwait(false);
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
		Console.WriteLine("VidCoderCLI pause");
		Console.WriteLine("VidCoderCLI resume");
	}

	private static void WriteError(string error)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine(error);
		Console.ResetColor();
	}
}
