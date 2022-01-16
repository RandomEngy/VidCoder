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
		private const string LocalAppDataFolderName = "VidCoder";

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

		public static string LocalAppFolder
		{
			get
			{
				string folder = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					LocalAppDataFolderName);

				if (CommonUtilities.Beta)
				{
					folder += "-Beta";
				}

				return folder;
			}
		}
	}
}
