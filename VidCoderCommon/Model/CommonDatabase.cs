using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Extensions;

namespace VidCoderCommon.Model
{
	public static class CommonDatabase
	{
		public const string ConfigDatabaseFileWithoutExtension = "VidCoder";
		public const string ConfigDatabaseFileExtension = ".sqlite";
		public const string ConfigDatabaseFile = ConfigDatabaseFileWithoutExtension + ConfigDatabaseFileExtension;

		/// <summary>
		/// Gets the database file path for a non-portable version.
		/// This is in %appdata% .
		/// </summary>
		public static string NonPortableDatabaseFile
		{
			get
			{
				string appDataFolder = CommonUtilities.AppFolder;

				if (!Directory.Exists(appDataFolder))
				{
					Directory.CreateDirectory(appDataFolder);
				}

				return Path.Combine(appDataFolder, ConfigDatabaseFile);
			}
		}

		public static void ExecuteNonQuery(string query, SQLiteConnection connection)
		{
			using (var command = new SQLiteCommand(connection))
			{
				command.CommandText = query;
				command.ExecuteNonQuery();
			}
		}
	}
}
