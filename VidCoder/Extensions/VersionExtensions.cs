using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Extensions
{
	public static class VersionExtensions
	{
		public static string ToShortString(this Version version)
		{
			if (version.Build == 0 && version.Revision == 0)
			{
				return $"{version.Major}.{version.Minor}";
			}

			if (version.Revision == 0)
			{
				return $"{version.Major}.{version.Minor}.{version.Build}";
			}

			return version.ToString();
		}
	}
}
