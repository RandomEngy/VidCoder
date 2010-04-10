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
    }
}
