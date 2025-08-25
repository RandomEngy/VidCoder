using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model.Job;

namespace VidCoder;

public static class VideoCodecUtilities
{
	public static string GetCodecFromDisplayName(string displayName)
	{
		int openParenIndex = displayName.IndexOf('(');
		if (openParenIndex < 0)
		{
			return displayName.Trim();
		}

		return displayName.Substring(0, openParenIndex).Trim();
	}

	public static JobConfiguration CreateJobConfiguration()
	{
		return new JobConfiguration
		{
			EnableQuickSyncDecoding = Config.EnableQuickSyncDecoding,
			UseQsvDecodeForNonQsvEncodes = Config.UseQsvDecodeForNonQsvEncodes,
			EnableNVDec = Config.EnableNVDec,
			EnableDirectXDecoding = Config.EnableDirectXDecoding
		};
	}
}

