using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public static class Players
	{
		public static List<IVideoPlayer> All
		{
			get
			{
				return new List<IVideoPlayer>
				       {
						   new VlcPlayer(),
						   new MpchcPlayer()
				       };
			}
		}

		public static List<IVideoPlayer> Installed
		{
			get
			{
				// When running under desktop bridge we cannot call other processes
				if (Utilities.IsRunningAsAppx)
				{
					return new List<IVideoPlayer>();
				}

				return All.Where(player => player.Installed).ToList();
			}
		}
	}
}
