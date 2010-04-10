using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Threading;

namespace VidCoder.Model
{
    public static class Presets
    {
        //private static readonly string PresetsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder\Prototype");
        private static readonly string UserPresetsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder\UserPresets");
        private static readonly string BuiltInPresetsPath = "BuiltInPresets.xml";
        //private static readonly string UserPresetsPath = Path.Combine(PresetsFolder, @"UserPresets.xml");
        //private static XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Preset>));
        private static XmlSerializer presetListSerializer = new XmlSerializer(typeof(List<Preset>));
        private static XmlSerializer presetSerializer = new XmlSerializer(typeof(Preset));
        private static object userPresetSync = new object();
        private static List<string> disallowedCharacters = new List<string> { "\\", "/", "\"", ":", "*", "?", "<", ">", "|" };

        public static List<Preset> BuiltInPresets
        {
            get
            {
                EnsureFolderCreated();
                using (FileStream stream = new FileStream(BuiltInPresetsPath, FileMode.Open, FileAccess.Read))
                {
                    var presetList = presetListSerializer.Deserialize(stream) as List<Preset>;
                    return presetList;
                }
            }

            set
            {
                EnsureFolderCreated();
                using (FileStream stream = new FileStream(BuiltInPresetsPath, FileMode.Create, FileAccess.Write))
                {
                    presetListSerializer.Serialize(stream, value);
                }
            }
        }

        public static List<Preset> UserPresets
        {
            get
            {
                EnsureFolderCreated();

                lock (userPresetSync)
                {
                    var result = new List<Preset>();

                    string[] presetFiles = Directory.GetFiles(UserPresetsFolder);

                    foreach (string presetFile in presetFiles)
                    {
                        if (Path.GetExtension(presetFile).ToLowerInvariant() == ".xml")
                        {
                            XDocument doc = XDocument.Load(presetFile);
                            XElement presetElement = doc.Element("UserPreset").Element("Preset");

                            using (XmlReader reader = presetElement.CreateReader())
                            {
                                var preset = presetSerializer.Deserialize(reader) as Preset;
                                result.Add(preset);
                            }
                        }
                    }

                    return result;
                }
            }

            set
            {
                EnsureFolderCreated();
                var presetXmlList = new List<Tuple<string, string>>();

                foreach (Preset preset in value)
                {
                    StringBuilder xmlBuilder = new StringBuilder();
                    using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
                    {
                        presetSerializer.Serialize(writer, preset);
                    }

                    string fileName = preset.Name;
                    if (preset.IsModified)
                    {
                        fileName += "_Modified";
                    }

                    presetXmlList.Add(Tuple.Create<string, string>(fileName, xmlBuilder.ToString()));
                }

                // Do the actual save asynchronously.
                ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, presetXmlList);
            }
        }

        //public static List<Preset> UserPresets1
        //{
        //    get
        //    {
        //        EnsureFolderCreated();

        //        try
        //        {
        //            lock (userPresetSync)
        //            {
        //                XDocument doc = XDocument.Load(UserPresetsPath);
        //                XElement presetArray = doc.Element("Presets").Element("ArrayOfPreset");

        //                using (XmlReader reader = presetArray.CreateReader())
        //                {
        //                    var presetList = presetListSerializer.Deserialize(reader) as List<Preset>;
        //                    return presetList;
        //                }
        //            }
        //        }
        //        catch (FileNotFoundException)
        //        {
        //        }

        //        return new List<Preset>();
        //    }

        //    set
        //    {
        //        EnsureFolderCreated();
        //        StringBuilder xmlBuilder = new StringBuilder();
        //        using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
        //        {
        //            presetListSerializer.Serialize(writer, value);
        //        }

        //        // Do the actual save asynchronously.
        //        ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, xmlBuilder);
        //    }
        //}

        private static void SaveUserPresetsBackground(object presetXmlListObject)
        {
            lock (userPresetSync)
            {
                string[] existingFiles = Directory.GetFiles(UserPresetsFolder);
                foreach (string existingFile in existingFiles)
                {
                    File.Delete(existingFile);
                }

                var presetXmlList = presetXmlListObject as List<Tuple<string, string>>;
                foreach (Tuple<string, string> listItem in presetXmlList)
                {
                    XElement element = XElement.Parse(listItem.Item2);
                    XDocument doc = new XDocument(
                        new XElement("UserPreset",
                            new XAttribute("Version", "1"),
                            element));

                    doc.Save(FindUserPresetPath(listItem.Item1));
                }
            }
        }

        private static string FindUserPresetPath(string baseName)
        {
            string cleanName = baseName;
            foreach (string disallowedChar in disallowedCharacters)
            {
                cleanName = cleanName.Replace(disallowedChar, "_");
            }

            string proposedPresetPath = Path.Combine(UserPresetsFolder, baseName + ".xml");

            if (File.Exists(proposedPresetPath))
            {
                for (int i = 1; i < 100; i++)
                {
                    proposedPresetPath = Path.Combine(UserPresetsFolder, baseName + "_" + i + ".xml");
                    if (!File.Exists(proposedPresetPath))
                    {
                        break;
                    }
                }
            }

            return proposedPresetPath;
        }

        //private static void SaveUserPresetsBackground1(object xmlBuilderObject)
        //{
        //    lock (userPresetSync)
        //    {
        //        var xmlBuilder = xmlBuilderObject as StringBuilder;

        //        XElement element = XElement.Parse(xmlBuilder.ToString());
        //        XDocument doc = new XDocument(
        //            new XElement("UserPreset",
        //                new XAttribute("Version", "1"),
        //                element));

        //        doc.Save(UserPresetsPath);
        //    }
        //}

        private static void EnsureFolderCreated()
        {
            if (!Directory.Exists(UserPresetsFolder))
            {
                Directory.CreateDirectory(UserPresetsFolder);
            }
        }
    }
}
