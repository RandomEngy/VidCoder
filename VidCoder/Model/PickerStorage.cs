using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using VidCoder.Resources;

namespace VidCoder.Model
{
    /// <summary>
    /// Storage class for pickers.
    /// </summary>
    public static class PickerStorage
    {
        public static string SerializePicker(Picker picker)
        {
	        return JsonConvert.SerializeObject(picker);
        }

        /// <summary>
        /// Load in a picker from an XML string.
        /// </summary>
        /// <param name="pickerXml">The XML of the picker to load in.</param>
        /// <param name="serializer">The XML serializer to use.</param>
        /// <returns>The loaded Picker.</returns>
        public static Picker ParsePickerXml(string pickerXml, XmlSerializer serializer)
        {
            try
            {
                using (var stringReader = new StringReader(pickerXml))
                {
                    using (var xmlReader = new XmlTextReader(stringReader))
                    {
						var picker = serializer.Deserialize(xmlReader) as Picker;

                        return picker;
                    }
                }
            }
            catch (XmlException exception)
            {
                System.Windows.MessageBox.Show(
                    MainRes.CouldNotLoadPickerMessage +
                    exception +
                    Environment.NewLine +
                    Environment.NewLine +
                    pickerXml);
            }

            return null;
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
				return JsonConvert.DeserializeObject<Picker>(pickerJson);
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

	    public static void UpgradePicker(Picker picker, int oldDatabaseVersion)
	    {
		    if (oldDatabaseVersion < 30)
		    {
			    UpgradePickerTo30(picker);
		    }

	        if (oldDatabaseVersion < 34)
	        {
	            UpgradePickerTo34(picker);
	        }

		    if (oldDatabaseVersion < 36)
		    {
			    UpgradePickerTo36(picker);
		    }
	    }

	    private static void UpgradePickerTo30(Picker picker)
	    {
		    if (picker.AudioSelectionMode == AudioSelectionMode.Language)
		    {
			    picker.AudioLanguageCodes = new List<string> { picker.AudioLanguageCode };
		    }

		    if (picker.SubtitleSelectionMode == SubtitleSelectionMode.Language)
		    {
			    picker.SubtitleLanguageCodes = new List<string> { picker.SubtitleLanguageCode };
			    picker.SubtitleBurnIn = picker.SubtitleLanguageBurnIn;
			    picker.SubtitleDefault = picker.SubtitleLanguageDefault;
		    }
		    else if (picker.SubtitleSelectionMode == SubtitleSelectionMode.All)
		    {
			    picker.SubtitleLanguageCodes = new List<string>();
		    }

		    if (picker.SubtitleSelectionMode == SubtitleSelectionMode.ForeignAudioSearch)
		    {
			    picker.SubtitleBurnIn = picker.SubtitleForeignBurnIn;
		    }
	    }

        private static void UpgradePickerTo34(Picker picker)
        {
            if (picker.SubtitleSelectionMode == SubtitleSelectionMode.ForeignAudioSearch)
            {
                picker.SubtitleForcedOnly = true;
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

        public static void SavePickers(List<string> pickerJsonList, SQLiteConnection connection)
        {
            Database.ExecuteNonQuery("DELETE FROM pickersJson", connection);

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
