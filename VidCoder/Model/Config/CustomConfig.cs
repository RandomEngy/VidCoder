using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
	using Model;

	public static class CustomConfig
	{
		public static List<string> AutoPauseProcesses
		{
			get
			{
				return new List<string>(Config.AutoPauseProcesses.Split('\x7f'));
			}

			set
			{
				Config.AutoPauseProcesses = string.Join("\x7f", value);
			}
		}

		public static WhenFileExists WhenFileExists
		{
			get
			{
				return (WhenFileExists)Enum.Parse(typeof(WhenFileExists), Config.WhenFileExists);
			}

			set
			{
				Config.WhenFileExists = value.ToString();
			}
		}

		public static WhenFileExists WhenFileExistsBatch
		{
			get
			{
				return (WhenFileExists)Enum.Parse(typeof(WhenFileExists), Config.WhenFileExistsBatch);
			}

			set
			{
				Config.WhenFileExistsBatch = value.ToString();
			}
		}

		public static AutoAudioType AutoAudio
		{
			get
			{
				return (AutoAudioType)Enum.Parse(typeof(AutoAudioType), Config.AutoAudio);
			}

			set
			{
				Config.AutoAudio = value.ToString();
			}
		}

		public static AutoSubtitleType AutoSubtitle
		{
			get
			{
				return (AutoSubtitleType)Enum.Parse(typeof(AutoSubtitleType), Config.AutoSubtitle);
			}

			set
			{
				Config.AutoSubtitle = value.ToString();
			}
		}

		public static PreviewDisplay PreviewDisplay
		{
			get
			{
				return (PreviewDisplay) Enum.Parse(typeof (PreviewDisplay), Config.PreviewDisplay);
			}

			set
			{
				Config.PreviewDisplay = value.ToString();
			}
		}
	}
}
