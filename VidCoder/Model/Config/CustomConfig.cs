using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder;

public static class CustomConfig
{
	public static UpdateMode UpdateMode
	{
		get => (UpdateMode)Enum.Parse(typeof(UpdateMode), Config.UpdateMode);
		set => Config.UpdateMode = value.ToString();
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
		if (useWorkerProcessOverride == null)
		{
			useWorkerProcessOverride = value;
		}
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

	public static DragDropOrder DragDropOrder
	{
		get => (DragDropOrder)Enum.Parse(typeof(DragDropOrder), Config.DragDropOrder);
		set => Config.DragDropOrder = value.ToString();
	}

	public static WatcherMode WatcherMode
	{
		get => (WatcherMode)Enum.Parse(typeof(WatcherMode), Config.WatcherMode);
		set => Config.WatcherMode = value.ToString();
	}

	public static PartFileNaming PartFileNaming
	{
		get => (PartFileNaming)Enum.Parse(typeof(PartFileNaming), Config.PartFileNaming);
		set => Config.PartFileNaming = value.ToString();
	}

	public static EncodeCompleteActionPersisted LastEncodeCompleteAction
	{
		get
		{
			string lastActionString = Config.LastEncodeCompleteAction;
			if (string.IsNullOrEmpty(lastActionString))
			{
				return new EncodeCompleteActionPersisted { ActionType = EncodeCompleteActionType.DoNothing };
			}

			return JsonSerializer.Deserialize<EncodeCompleteActionPersisted>(Config.LastEncodeCompleteAction);
		}
		set => Config.LastEncodeCompleteAction = JsonSerializer.Serialize(value);
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
