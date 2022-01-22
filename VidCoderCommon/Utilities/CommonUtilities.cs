using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon
{
	public static class CommonUtilities
	{
		private const string AppDataFolderName = "VidCoder";

		public static bool Beta
		{
			get
			{
#if BETA
				return true;
#else
				return false;
#endif
			}
		}

		public static bool DebugMode
		{
			get
			{
#if DEBUG
				return true;
#else
				return false;
#endif
			}
		}

		public static string SquirrelAppId
		{
			get
			{
				return Beta ? "VidCoder.Beta" : "VidCoder.Stable";
			}
		}

		public static string LocalAppFolder
		{
			get
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					SquirrelAppId);
			}
		}

		public static string AppFolder
		{
			get
			{
				return GetAppFolder(Beta);
			}
		}

		public static string GetAppFolder(bool beta)
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);

			if (beta)
			{
				folder += "-Beta";
			}

			return folder;
		}

		public static string LogsFolder
		{
			get
			{
				string logFolder = Path.Combine(AppFolder, "Logs");
				if (!Directory.Exists(logFolder))
				{
					Directory.CreateDirectory(logFolder);
				}

				return logFolder;
			}
		}
	}
}
