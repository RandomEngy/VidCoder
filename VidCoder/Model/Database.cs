using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	public static class Database
	{
		private const string ConfigDatabaseFile = "VidCoder.sqlite";

		private static SQLiteConnection connection;

		public static string DatabaseFile
		{
			get
			{
				string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Utilities.AppDataFolderName);

				if (!Directory.Exists(appDataFolder))
				{
					Directory.CreateDirectory(appDataFolder);
				}

				return Path.Combine(appDataFolder, ConfigDatabaseFile);
			}
		}

		public static string ConnectionString
		{
			get
			{
				return "Data Source=" + DatabaseFile;
			}
		}

		public static SQLiteConnection Connection
		{
			get
			{
				if (connection == null)
				{
					connection = CreateConnection();
				}

				return connection;
			}
		}

		public static SQLiteConnection CreateConnection()
		{
			if (!Directory.Exists(Utilities.AppFolder))
			{
				Directory.CreateDirectory(Utilities.AppFolder);
			}

			bool newDataFile = !File.Exists(DatabaseFile);

			var newConnection = new SQLiteConnection(ConnectionString);
			newConnection.Open();

			if (newDataFile)
			{
				CreateTables(newConnection);

				var settingsList = new Dictionary<string, string> {{"Version", Utilities.CurrentDatabaseVersion.ToString()}};
				AddSettingsList(newConnection, settingsList);
			}

			return newConnection;
		}

		public static void CreateTables(SQLiteConnection connection)
		{
			Database.ExecuteNonQuery("CREATE TABLE presetsXml (" +
				"xml TEXT)", connection);

			Database.ExecuteNonQuery(
				"CREATE TABLE settings (" +
				"name TEXT, " +
				"value TEXT)", connection);
		}

		public static void ExecuteNonQuery(string query, SQLiteConnection connection)
		{
			using (var command = new SQLiteCommand(connection))
			{
				command.CommandText = query;
				command.ExecuteNonQuery();
			}
		}

		private static void AddSettingsList(SQLiteConnection connection, Dictionary<string, string> settingsList)
		{
			using (var settingsInsert = new SQLiteCommand(connection))
			{
				// Add settings
				var settingsName = new SQLiteParameter();
				var settingsValue = new SQLiteParameter();

				settingsInsert.CommandText = "INSERT INTO settings (name, value) VALUES (?, ?)";
				settingsInsert.Parameters.Add(settingsName);
				settingsInsert.Parameters.Add(settingsValue);

				foreach (KeyValuePair<string, string> pair in settingsList)
				{
					settingsName.Value = pair.Key;
					settingsValue.Value = pair.Value;

					settingsInsert.ExecuteNonQuery();
				}
			}
		}
	}
}
