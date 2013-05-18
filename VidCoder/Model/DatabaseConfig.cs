using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using VidCoder.Services;

namespace VidCoder.Model
{
	using System.Globalization;
	using System.Threading;

	public static class DatabaseConfig
	{
		public static int Version
		{
			get
			{
				return GetConfig("Version", Utilities.CurrentDatabaseVersion, Database.ThreadLocalConnection);
			}
		}

		public static bool GetConfig(string configName, bool defaultValue, SQLiteConnection connection)
		{
			string configValue = GetConfigStringRaw(configName, connection);
			if (configValue != null)
			{
				return bool.Parse(configValue);
			}

			return defaultValue;
		}

		public static int GetConfig(string configName, int defaultValue, SQLiteConnection connection)
		{
			string configValue = GetConfigStringRaw(configName, connection);
			if (configValue != null)
			{
				return int.Parse(configValue);
			}

			return defaultValue;
		}

		public static double GetConfig(string configName, double defaultValue, SQLiteConnection connection)
		{
			string configValue = GetConfigStringRaw(configName, connection);
			if (configValue != null)
			{
				return double.Parse(configValue);
			}

			return defaultValue;
		}

		public static string GetConfig(string configName, string defaultValue, SQLiteConnection connection)
		{
			string configValue = GetConfigStringRaw(configName, connection);
			if (configValue != null)
			{
				return configValue;
			}

			return defaultValue;
		}

		private static string GetConfigStringRaw(string configName, SQLiteConnection connection)
		{
			using (var command = new SQLiteCommand("SELECT value FROM settings WHERE name = '" + configName + "'", connection))
			{
				using (SQLiteDataReader reader = command.ExecuteReader())
				{
					if (reader.Read())
					{
						if (reader.IsDBNull(0))
						{
							return string.Empty;
						}

						return reader.GetString(0);
					}
				}
			}

			return null;
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
			SetConfigValue(configName, value.ToString(CultureInfo.InvariantCulture), connection);
		}

		public static void SetConfigValue(string configName, bool value)
		{
			SetConfigValue(configName, value, Database.ThreadLocalConnection);
		}

		public static void SetConfigValue(string configName, int value, SQLiteConnection connection)
		{
			SetConfigValue(configName, value.ToString(CultureInfo.InvariantCulture), connection);
		}

		public static void SetConfigValue(string configName, int value)
		{
			SetConfigValue(configName, value, Database.ThreadLocalConnection);
		}

		public static void SetConfigValue(string configName, double value, SQLiteConnection connection)
		{
			SetConfigValue(configName, value.ToString(CultureInfo.InvariantCulture), connection);
		}

		public static void SetConfigValue(string configName, double value)
		{
			SetConfigValue(configName, value, Database.ThreadLocalConnection);
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

		public static void SetConfigValue(string configName, string configValue)
		{
			SetConfigValue(configName, configValue, Database.ThreadLocalConnection);
		}
	}
}
