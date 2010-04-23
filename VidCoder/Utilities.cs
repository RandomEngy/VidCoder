using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.IO;

namespace VidCoder
{
    public static class Utilities
    {
        public const string UpdateInfoUrl = "http://engy.us/VidCoder/latest.xml";
        public const string AppDataFolderName = "VidCoder";

        private static Dictionary<string, double> defaultQueueColumnSizes = new Dictionary<string, double>
        {
            {"Source", 200},
            {"Title", 35},
            {"Chapters", 60},
            {"Destination", 200},
            {"VideoEncoder", 100},
            {"AudioEncoder", 100}
        };

        public static string CurrentVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public static int CompareVersions(string versionA, string versionB)
        {
            string[] stringPartsA = versionA.Split('.');
            string[] stringPartsB = versionB.Split('.');

            int[] intPartsA = new int[stringPartsA.Length];
            int[] intPartsB = new int[stringPartsB.Length];

            for (int i = 0; i < intPartsA.Length; i++)
            {
                intPartsA[i] = int.Parse(stringPartsA[i]);
            }

            for (int i = 0; i < intPartsB.Length; i++)
            {
                intPartsB[i] = int.Parse(stringPartsB[i]);
            }

            int compareLength = Math.Min(intPartsA.Length, intPartsB.Length);

            for (int i = 0; i < compareLength; i++)
            {
                if (intPartsA[i] > intPartsB[i])
                {
                    return 1;
                }
                else if (intPartsA[i] < intPartsB[i])
                {
                    return -1;
                }
            }

            if (intPartsA.Length > intPartsB.Length)
            {
                for (int i = compareLength; i < intPartsA.Length; i++)
                {
                    if (intPartsA[i] > 0)
                    {
                        return 1;
                    }
                }
            }

            if (intPartsA.Length < intPartsB.Length)
            {
                for (int i = compareLength; i < intPartsB.Length; i++)
                {
                    if (intPartsB[i] > 0)
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }

        public static int CurrentProcessInstances
        {
            get
            {
                Process[] processList = Process.GetProcessesByName("VidCoder");
                return processList.Length;
            }
        }

        public static string AppFolder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
            }
        }

        public static string UpdatesFolder
        {
            get
            {
                string updatesFolder;

                updatesFolder = Path.Combine(AppFolder, "Updates");

                if (!Directory.Exists(updatesFolder))
                {
                    Directory.CreateDirectory(updatesFolder);
                }

                return updatesFolder;
            }
        }

        public static Dictionary<string, double> DefaultQueueColumnSizes
        {
            get
            {
                return defaultQueueColumnSizes;
            }
        }

        public static bool IsValidQueueColumn(string columnId)
        {
            return defaultQueueColumnSizes.ContainsKey(columnId);
        }

        /// <summary>
        /// Parse a size list in the format {column id 1}:{width 1}|{column id 2}:{width 2}|...
        /// </summary>
        /// <param name="listString">The string to parse.</param>
        /// <returns>The parsed list of sizes.</returns>
        public static List<Tuple<string, double>> ParseQueueColumnList(string listString)
        {
            var resultList = new List<Tuple<string, double>>();

            string[] columnSettings = listString.Split('|');
            foreach (string columnSetting in columnSettings)
            {
                if (!string.IsNullOrWhiteSpace(columnSetting))
                {
                    string[] settingParts = columnSetting.Split(':');
                    if (settingParts.Length == 2)
                    {
                        double columnWidth;
                        string columnId = settingParts[0];
                        if (IsValidQueueColumn(columnId) && double.TryParse(settingParts[1], out columnWidth))
                        {
                            resultList.Add(new Tuple<string, double>(columnId, columnWidth));
                        }
                    }
                }
            }

            return resultList;
        }
    }
}
