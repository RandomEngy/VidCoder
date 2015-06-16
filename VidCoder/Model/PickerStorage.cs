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
            {
                using (SQLiteDataReader reader = selectPickersCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string pickerJson = reader.GetString("json");
						result.Add(ParsePickerJson(pickerJson));
                    }
                }
            }

            return result;
        }

        private static void SavePickers(List<string> pickerJsonList, SQLiteConnection connection)
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
