using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using VidCoder.Services;

namespace VidCoder.Model
{
	public static class DatabaseConfig
	{
		private static Dictionary<string, string> configDefaults;

		static DatabaseConfig()
		{
			configDefaults = new Dictionary<string, string>
			{
				{"Version", Utilities.CurrentDatabaseVersion.ToString()},
				{"EncodeJobs", string.Empty},
				{"EncodeJobs2", string.Empty},
				{Updater.UpdateInProgress, "false"},
				{Updater.UpdateVersion, string.Empty},
				{Updater.UpdateInstallerLocation, string.Empty},
				{Updater.UpdateChangelogLocation, string.Empty}
			};
		}

		public static bool GetConfigBool(string configName, SQLiteConnection connection)
		{
			return bool.Parse(GetConfigString(configName, connection));
		}

		public static int GetConfigInt(string configName, SQLiteConnection connection)
		{
			return int.Parse(GetConfigString(configName, connection));
		}

		public static string GetConfigString(string configName, SQLiteConnection connection)
		{
			using (var command = new SQLiteCommand("SELECT value FROM settings WHERE name = '" + configName + "'", connection))
			{
				using (SQLiteDataReader reader = command.ExecuteReader())
				{
					if (reader.Read())
					{
						return reader.GetString(0);
					}
				}
			}

			// The setting does not exist in the DB, we need to insert the default value.

			if (configDefaults.ContainsKey(configName))
			{
				AddConfigValue(configName, configDefaults[configName], connection);

				return configDefaults[configName];
			}

			throw new ArgumentException("Config value not found: " + configName, "configName");
		}

		private static void AddConfigValue(string configName, string configValue, SQLiteConnection connection)
		{
			using (var settingsInsert = new SQLiteCommand(connection))
			{
				settingsInsert.CommandText = "INSERT INTO settings (name, value) VALUES (?, ?)";
				settingsInsert.Parameters.Add("name", DbType.String).Value = configName;
				settingsInsert.Parameters.Add("value", DbType.String).Value = configValue;

				settingsInsert.ExecuteNonQuery();
			}
		}

		public static void SetConfigValue(string configName, bool value, SQLiteConnection connection)
		{
			SetConfigValue(configName, value.ToString(), connection);
		}

		public static void SetConfigValue(string configName, int value, SQLiteConnection connection)
		{
			SetConfigValue(configName, value.ToString(), connection);
		}

		public static void SetConfigValue(string configName, string configValue, SQLiteConnection connection)
		{
			var command = new SQLiteCommand("UPDATE settings SET value = ? WHERE name = ?", connection);
			command.Parameters.Add("value", DbType.String).Value = configValue;
			command.Parameters.Add("name", DbType.String).Value = configName;

			if (command.ExecuteNonQuery() == 0)
			{
				// If the setting did not exist, add it
				AddConfigValue(configName, configValue, connection);
			}
		}
	}
}
