using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder.Extensions
{
	public static class WatchedFileStatusExtensions
	{
		public static WatchedFileStatusLive ToLive(this WatchedFileStatus status)
		{
			switch (status)
			{
				case WatchedFileStatus.Planned:
					return WatchedFileStatusLive.Planned;
				case WatchedFileStatus.Canceled:
					return WatchedFileStatusLive.Canceled;
				case WatchedFileStatus.Succeeded:
					return WatchedFileStatusLive.Succeeded;
				case WatchedFileStatus.Failed:
					return WatchedFileStatusLive.Failed;
				case WatchedFileStatus.Skipped:
					return WatchedFileStatusLive.Skipped;
				case WatchedFileStatus.Output:
					return WatchedFileStatusLive.Output;
				default:
					throw new ArgumentException("The WatchedFileStatus is unrecognized.");
			}
		}
	}
}
