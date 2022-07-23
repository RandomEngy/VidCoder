using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;

namespace VidCoder.Model
{
    /// <summary>
    /// Storage class for pickers.
    /// </summary>
    public static class PickerStorage
    {
        public static string SerializePicker(Picker picker)
        {
	        return JsonSerializer.Serialize(picker, JsonOptions.WithUpgraders);
        }

		/// <summary>
		/// Load in a picker from an JSON string.
		/// </summary>
		/// <param name="pickerJson">The JSON of the picker to load in.</param>
		/// <returns>The loaded Picker.</returns>
	    public static Picker ParsePickerJson(string pickerJson)
	    {
			try
			{
				return JsonSerializer.Deserialize<Picker>(pickerJson, JsonOptions.WithUpgraders);
			}
			catch (Exception exception)
			{
				System.Windows.MessageBox.Show(
					MainRes.CouldNotLoadPickerMessage +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					pickerJson);
			}

			return null;
	    }

        /// <summary>
        /// Gets or sets the stored picker list. Does not include the "None" picker.
        /// </summary>
        public static List<Picker> PickerList
        {
            get { return GetPickerListFromDb(); }

            set
            {
                List<string> pickerJsonList = value.Select(SerializePicker).ToList();

                using (var transaction = Database.Connection.BeginTransaction())
                {
					SavePickers(pickerJsonList, Database.Connection);
                    transaction.Commit();
                }
            }
        }

        public static List<Picker> GetPickerListFromDb()
        {
            var result = new List<Picker>();

            using (var selectPickersCommand = new SQLiteCommand("SELECT * FROM pickersJson", Database.Connection))
			using (SQLiteDataReader reader = selectPickersCommand.ExecuteReader())
			{
                while (reader.Read())
                {
                    string pickerJson = reader.GetString("json");
					result.Add(ParsePickerJson(pickerJson));
                }
            }

            return result;
        }

	    public static void UpgradePickerUpTo37(Picker picker, int oldDatabaseVersion)
	    {
		    if (oldDatabaseVersion < 36)
		    {
			    UpgradePickerTo36(picker);
		    }

			if (oldDatabaseVersion < 37)
			{
				UpgradePickerTo37(picker);
			}
	    }

		public static void UpgradePicker(Picker picker, int oldDatabaseVersion)
		{
			if (oldDatabaseVersion < 41)
			{
				UpgradePickerTo41(picker);
			}

			if (oldDatabaseVersion < 43)
			{
				UpgradePickerTo43(picker);
			}

			if (oldDatabaseVersion < 45)
			{
				UpgradePickerTo45(picker);
			}

			if (oldDatabaseVersion < 47)
			{
				UpgradePickerTo47(picker);
			}
		}

	    private static void UpgradePickerTo36(Picker picker)
	    {
		    switch (picker.EncodingPreset)
		    {
				case "HighProfile":
					picker.EncodingPreset = "High Profile";
					break;
				case "AndroidTablet":
					picker.EncodingPreset = "Android Tablet";
					break;
				case "AppleiPod":
					picker.EncodingPreset = "iPod";
					break;
				case "AppleiPhone4":
				case "AppleiPhoneTouch":
					picker.EncodingPreset = "iPhone & iPod touch";
					break;
				case "AppleiPad":
					picker.EncodingPreset = "iPad";
					break;
				case "AppleTV2":
					picker.EncodingPreset = "AppleTV 2";
					break;
				case "AppleTV3":
					picker.EncodingPreset = "AppleTV 3";
					break;
				case "WindowsPhone7":
				case "WindowsPhone8":
					picker.EncodingPreset = "Windows Phone 8";
					break;
				case "Xbox360":
					picker.EncodingPreset = "Xbox Legacy 1080p30 Surround";
					break;
				default:
					break;
			}
	    }

		private static void UpgradePickerTo37(Picker picker)
		{
			if (picker.ExtensionData.GetBool("TimeRangeSelectEnabled"))
			{
				picker.PickerTimeRangeMode = PickerTimeRangeMode.Time;
			}
			else
			{
				picker.PickerTimeRangeMode = PickerTimeRangeMode.All;
			}

			picker.ExtensionData.Remove("TimeRangeSelectEnabled");
		}

		private static void UpgradePickerTo39(Picker picker)
		{
			string configurationOutputDirectory = DatabaseConfig.Get<string>("AutoNameOutputFolder", null);
			if (picker.ExtensionData.GetBool("OutputDirectoryOverrideEnabled"))
			{
				picker.OutputDirectory = picker.ExtensionData.GetString("OutputDirectoryOverride");
			}
			else if (!string.IsNullOrEmpty(configurationOutputDirectory))
			{
				picker.OutputDirectory = configurationOutputDirectory;
			}
			else
			{
				picker.OutputDirectory = null;
			}

			bool? outputToSourceDirectoryNullable = picker.ExtensionData.GetNullableBool("OutputToSourceDirectory");
			if (outputToSourceDirectoryNullable == null)
			{
				picker.OutputToSourceDirectory = DatabaseConfig.Get<bool>("OutputToSourceDirectory", false);
			}
			else
			{
				picker.OutputToSourceDirectory = outputToSourceDirectoryNullable.Value;
			}

			bool? preserveFolderStructureInBatchNullable = picker.ExtensionData.GetNullableBool("PreserveFolderStructureInBatch");
			if (preserveFolderStructureInBatchNullable == null)
			{
				picker.PreserveFolderStructureInBatch = DatabaseConfig.Get<bool>("PreserveFolderStructureInBatch", false);
			}
			else
			{
				picker.PreserveFolderStructureInBatch = preserveFolderStructureInBatchNullable.Value;
			}

			bool nameFormatOverrideEnabled = picker.ExtensionData.GetBool("NameFormatOverrideEnabled");
			picker.UseCustomFileNameFormat = nameFormatOverrideEnabled;
			if (nameFormatOverrideEnabled)
			{
				picker.OutputFileNameFormat = picker.ExtensionData.GetString("NameFormatOverride");
			}
			else
			{
				picker.OutputFileNameFormat = null;
			}

			string whenFileExistsSingleString = DatabaseConfig.Get<string>("WhenFileExists", "Prompt");
			picker.WhenFileExistsSingle = (WhenFileExists)Enum.Parse(typeof(WhenFileExists), whenFileExistsSingleString);

			string whenFileExistsBatchString = DatabaseConfig.Get<string>("WhenFileExistsBatch", "AutoRename");
			picker.WhenFileExistsBatch = (WhenFileExists)Enum.Parse(typeof(WhenFileExists), whenFileExistsBatchString);

			if (picker.ExtensionData != null)
			{
				picker.ExtensionData.Remove("OutputToSourceDirectory");
				picker.ExtensionData.Remove("PreserveFolderStructureInBatch");
				picker.ExtensionData.Remove("OutputDirectoryOverrideEnabled");
				picker.ExtensionData.Remove("OutputDirectoryOverride");
				picker.ExtensionData.Remove("NameFormatOverrideEnabled");
				picker.ExtensionData.Remove("NameFormatOverride");
			}
		}

		public static void UpgradePickersTo39(List<Picker> pickers)
		{
			// Upgrade all the existing pickers
			foreach (Picker picker in pickers)
			{
				UpgradePickerTo39(picker);
			}

			// And if we have the "default" picker selected right now, add a new one with our settings copied over.
			int lastPickerIndex = DatabaseConfig.Get<int>("LastPickerIndex", 0);
			if (lastPickerIndex == 0)
			{
				bool useCustomNameFormat = DatabaseConfig.Get<bool>("AutoNameCustomFormat", false);
				bool outputToSourceDirectory = DatabaseConfig.Get<bool>("OutputToSourceDirectory", false);
				bool preserveFolderStructureInBatch = DatabaseConfig.Get<bool>("PreserveFolderStructureInBatch", false);
				string whenFileExistsSingleString = DatabaseConfig.Get<string>("WhenFileExists", "Prompt");
				WhenFileExists whenFileExistsSingle = (WhenFileExists)Enum.Parse(typeof(WhenFileExists), whenFileExistsSingleString);
				string whenFileExistsBatchString = DatabaseConfig.Get<string>("WhenFileExistsBatch", "AutoRename");
				WhenFileExists whenFileExistsBatch = (WhenFileExists)Enum.Parse(typeof(WhenFileExists), whenFileExistsBatchString);

				var newPicker = new Picker
				{
					Name = CreateCustomPickerName(pickers),
					UseCustomFileNameFormat = useCustomNameFormat,
					OutputToSourceDirectory = outputToSourceDirectory,
					PreserveFolderStructureInBatch = preserveFolderStructureInBatch,
					WhenFileExistsSingle = whenFileExistsSingle,
					WhenFileExistsBatch = whenFileExistsBatch
				};

				if (useCustomNameFormat)
				{
					string outputFileNameFormat = DatabaseConfig.Get<string>("AutoNameCustomFormatString", null);
					newPicker.OutputFileNameFormat = outputFileNameFormat;
				}

				string outputDirectory = DatabaseConfig.Get<string>("AutoNameOutputFolder", null);
				if (!string.IsNullOrEmpty(outputDirectory))
				{
					newPicker.OutputDirectory = outputDirectory;
				}

				pickers.Insert(0, newPicker);

				DatabaseConfig.Set<int>("LastPickerIndex", 1);
			}
		}

		private static void UpgradePickerTo41(Picker picker)
		{
			bool subtitleBurnIn = picker.ExtensionData.GetBool("SubtitleBurnIn");

			if (picker.SubtitleSelectionMode == SubtitleSelectionMode.ForeignAudioSearch)
			{
				picker.SubtitleSelectionMode = SubtitleSelectionMode.None;
				picker.SubtitleAddForeignAudioScan = true;

				if (subtitleBurnIn)
				{
					picker.SubtitleBurnInSelection = SubtitleBurnInSelection.ForeignAudioTrack;
				}
				else
				{
					picker.SubtitleBurnInSelection = SubtitleBurnInSelection.None;
				}
			}
			else
			{
				if (subtitleBurnIn)
				{
					picker.SubtitleBurnInSelection = SubtitleBurnInSelection.First;
				}
				else
				{
					picker.SubtitleBurnInSelection = SubtitleBurnInSelection.None;
				}
			}

			picker.ExtensionData?.Remove("SubtitleBurnIn");
		}

		private static void UpgradePickerTo43(Picker picker)
		{
			// Somehow the extension data from some old pickers hadn't gotten cleared out.
			picker.ExtensionData = null;
		}

		private static void UpgradePickerTo45(Picker picker)
		{
			bool deleteSourceFiles = DatabaseConfig.Get<bool>("DeleteSourceFilesOnClearingCompleted", false);
			bool recycle = DatabaseConfig.Get<string>("DeleteSourceFilesMode", "Recycle") == "Recycle";

			if (deleteSourceFiles)
			{
				picker.SourceFileRemoval = recycle ? SourceFileRemoval.Recycle : SourceFileRemoval.Delete;
				picker.SourceFileRemovalTiming = SourceFileRemovalTiming.AfterClearingCompletedItems;
				picker.SourceFileRemovalConfirmation = !recycle;
			}
		}

		private static void UpgradePickerTo47(Picker picker)
		{
			string videoFileExtensions = DatabaseConfig.Get<string>("VideoFileExtensions", null);
			if (videoFileExtensions != null)
			{
				picker.VideoFileExtensions = videoFileExtensions;
			}
		}

		public static string CreateCustomPickerName(List<Picker> existingPickers)
		{
			if (existingPickers.Any(p => p.Name == CommonRes.Custom))
			{
				for (int i = 2; i < 500; i++)
				{
					string newName = string.Format(PickerRes.CustomPickerNameTemplate, i);
					if (!existingPickers.Any(p => p.Name == newName))
					{
						return newName;
					}
				}
			}

			return CommonRes.Custom;
		}

		public static void SavePickers(List<string> pickerJsonList, SQLiteConnection connection)
        {
            CommonDatabase.ExecuteNonQuery("DELETE FROM pickersJson", connection);

			var insertCommand = new SQLiteCommand("INSERT INTO pickersJson (json) VALUES (?)", connection);
            SQLiteParameter insertJsonParam = insertCommand.Parameters.Add("json", DbType.String);

			foreach (string pickerJson in pickerJsonList)
            {
				insertJsonParam.Value = pickerJson;
                insertCommand.ExecuteNonQuery();
            }
        }
    }
}
