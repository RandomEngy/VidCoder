namespace VidCoderWindowlessCLI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

class Program
{
	static void Main(string[] args)
	{
		string programFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string passArguments = string.Join(" ", args.Select(WrapArgument));

		Process.Start(new ProcessStartInfo(Path.Combine(programFolder, "VidCoderCLI.exe"))
		{
			Arguments = passArguments,
			WindowStyle = ProcessWindowStyle.Hidden,
			CreateNoWindow = true
		});
	}

	private static string WrapArgument(string arg)
	{
		if (arg.EndsWith(@"\"))
		{
			return "\"" + arg + "\\\"";
		}

		return "\"" + arg + "\"";
	}
}
