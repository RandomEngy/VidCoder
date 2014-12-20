using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using VidCoder.Resources;

namespace VidCoder.Model
{
    /// <summary>
    /// Storage class for pickers.
    /// </summary>
    public static class PickerStorage
    {
        private static XmlSerializer pickerSerializer = new XmlSerializer(typeof(Picker));

        public static string SerializePicker(Picker picker)
        {
            var xmlBuilder = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
            {
                pickerSerializer.Serialize(writer, picker);
            }

            return xmlBuilder.ToString();
        }

        /// <summary>
        /// Load in a picker from an XML string.
        /// </summary>
        /// <param name="pickerXml">The XML of the picker to load in.</param>
        /// <returns>The loaded Picker.</returns>
        public static Picker LoadPickerXmlString(string pickerXml)
        {
            try
            {
                using (var stringReader = new StringReader(pickerXml))
                {
                    using (var xmlReader = new XmlTextReader(stringReader))
                    {
                        var picker = pickerSerializer.Deserialize(xmlReader) as Picker;

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
        /// Gets or sets the stored picker list. Does not include the "None" picker.
        /// </summary>
        public static List<Picker> PickerList
        {
            get { return GetPickerListFromDb(); }

            set
            {
                List<string> pickerXmlList = value.Select(SerializePicker).ToList();

                using (var transaction = Database.Connection.BeginTransaction())
                {
                    SavePickers(pickerXmlList, Database.Connection);
                    transaction.Commit();
                }
            }
        }

        public static List<Picker> GetPickerListFromDb()
        {
            var result = new List<Picker>();

            using (var selectPickersCommand = new SQLiteCommand("SELECT * FROM pickersXml", Database.Connection))
            {
                using (SQLiteDataReader reader = selectPickersCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string pickerXml = reader.GetString("xml");
                        result.Add(LoadPickerXmlString(pickerXml));
                    }
                }
            }

            return result;
        }

        private static void SavePickers(List<string> pickerXmlList, SQLiteConnection connection)
        {
            Database.ExecuteNonQuery("DELETE FROM pickersXml", connection);

            var insertCommand = new SQLiteCommand("INSERT INTO pickersXml (xml) VALUES (?)", connection);
            SQLiteParameter insertXmlParam = insertCommand.Parameters.Add("xml", DbType.String);

            foreach (string pickerXml in pickerXmlList)
            {
                insertXmlParam.Value = pickerXml;
                insertCommand.ExecuteNonQuery();
            }
        }
    }
}
