using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using VidCoder.Resources;

namespace VidCoder.Model
{
    public static class Pickers
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

        public static List<Picker> PickerList
        {
            get { return GetPickerListFromDb(); }
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
    }
}
