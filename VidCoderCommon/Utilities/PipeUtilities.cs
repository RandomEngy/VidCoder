using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderCommon.Utilities;

public static class PipeUtilities
{
	/// <summary>
	/// To prevent cross-contamination of pipes between users, we append the username to the pipe name.
	/// We sanitize the username to ensure it only contains valid characters for a pipe name.
	/// </summary>
	public static string UserPipeSuffix => SanitizePipeSegment(Environment.UserName);

	private static string SanitizePipeSegment(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return string.Empty;
		}

		return new string(s.Select(c => (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') ? c : '_').ToArray());
	}
}
