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
				return All.Where(player => player.Installed).ToList();
			}
		}
	}
}
