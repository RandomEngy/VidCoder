using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VidCoder.Model;

namespace VidCoder
{
	public static class CustomConfig
	{
		public static UpdatePromptTiming UpdatePromptTiming
		{
			get => (UpdatePromptTiming)Enum.Parse(typeof(UpdatePromptTiming), Config.UpdatePromptTiming);
			set => Config.UpdatePromptTiming = value.ToString();
		}

		public static List<string> AutoPauseProcesses
		{
			get
			{
				return new List<string>(Config.AutoPauseProcesses.Split(new char[] {'\x7f'}, StringSplitOptions.RemoveEmptyEntries));
			}

			set
			{
				Config.AutoPauseProcesses = string.Join("\x7f", value);
			}
		}

		public static HashSet<string> CollapsedBuiltInFolders
		{
			get
			{
				return new HashSet<string>(Config.CollapsedBuiltInFolders.Split(new char[] { '☃' }, StringSplitOptions.RemoveEmptyEntries));
			}

			set
			{
				Config.CollapsedBuiltInFolders = string.Join("☃", value);
			}
		}

		private static bool? useWorkerProcessOverride;
		public static bool UseWorkerProcess
		{
			get
			{
				if (useWorkerProcessOverride != null)
				{
					return useWorkerProcessOverride.Value;
				}

				return Config.UseWorkerProcess;
			}
		}

		public static void SetWorkerProcessOverride(bool value)
		{
			useWorkerProcessOverride = value;
		}

		[Obsolete("Use Picker.WhenFileExistsSingle instead.")]
		public static WhenFileExists WhenFileExists
		{
			get => (WhenFileExists)Enum.Parse(typeof(WhenFileExists), Config.WhenFileExists);
			set => Config.WhenFileExists = value.ToString();
		}

		[Obsolete("Use Picker.WhenFileExistsBatch instead.")]
		public static WhenFileExists WhenFileExistsBatch
		{
			get => (WhenFileExists)Enum.Parse(typeof(WhenFileExists), Config.WhenFileExistsBatch);
			set => Config.WhenFileExistsBatch = value.ToString();
		}

		public static AudioSelectionMode AutoAudio
		{
			get => (AudioSelectionMode)Enum.Parse(typeof(AudioSelectionMode), Config.AutoAudio);
			set => Config.AutoAudio = value.ToString();
		}

		public static SubtitleSelectionMode AutoSubtitle
		{
			get => (SubtitleSelectionMode)Enum.Parse(typeof(SubtitleSelectionMode), Config.AutoSubtitle);
			set => Config.AutoSubtitle = value.ToString();
		}

		public static PreviewDisplay PreviewDisplay
		{
			get => (PreviewDisplay) Enum.Parse(typeof (PreviewDisplay), Config.PreviewDisplay);
			set => Config.PreviewDisplay = value.ToString();
		}

		public static AppThemeChoice AppTheme
		{
			get => (AppThemeChoice)Enum.Parse(typeof(AppThemeChoice), Config.AppTheme);
			set => Config.AppTheme = value.ToString();
		}

		public static DeleteSourceFilesMode DeleteSourceFilesMode
		{
			get => (DeleteSourceFilesMode)Enum.Parse(typeof(DeleteSourceFilesMode), Config.DeleteSourceFilesMode);
			set => Config.DeleteSourceFilesMode = value.ToString();
		}

		public static ProcessPriorityClass WorkerProcessPriority
		{
			get
			{
				switch (Config.WorkerProcessPriority)
				{
					case "High":
						return ProcessPriorityClass.High;
					case "AboveNormal":
						return ProcessPriorityClass.AboveNormal;
					case "Normal":
						return ProcessPriorityClass.Normal;
					case "BelowNormal":
						return ProcessPriorityClass.BelowNormal;
					case "Idle":
						return ProcessPriorityClass.Idle;
					default:
						throw new ArgumentException("Priority class not recognized: " + Config.WorkerProcessPriority);
				}
			}
		}
	}
}
