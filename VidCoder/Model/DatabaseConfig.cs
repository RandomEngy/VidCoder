using System;
using System.Data;
using System.Data.SQLite;
using System.Globalization;

namespace VidCoder.Model
{
	/// <summary>
	/// Accesses config values in database directly without any local caching.
	/// </summary>
	public static class DatabaseConfig
	{
		public static int Version
		{
			get
			{
				return Get("Version", Utilities.CurrentDatabaseVersion, Database.ThreadLocalConnection);
			}
		}

		/// <summary>
		/// Gets a config value from the database.
		/// </summary>
		/// <typeparam name="T">The type of configuration value. (bool, string, int, double)</typeparam>
		/// <param name="configName">The configuration key.</param>
		/// <param name="defaultValue">The default value to use if it's not set.</param>
		/// <param name="connection">The connection to use.</param>
		/// <returns>The </returns>
		public static T Get<T>(string configName, T defaultValue, SQLiteConnection connection = null)
		{
			if (connection == null)
			{
				connection = Database.ThreadLocalConnection;
			}

			string configValue = GetConfigStringRaw(configName, connection);
			if (configValue == null)
			{
				return defaultValue;
			}

			Type type = typeof (T);
			if (type == typeof (bool))
			{
				return (T)(object)bool.Parse(configValue);
			}

			if (type == typeof (int))
			{
				return (T)(object)int.Parse(configValue, CultureInfo.InvariantCulture);
			}

			if (type == typeof (double))
			{
				return (T)(object)double.Parse(configValue, CultureInfo.InvariantCulture);
			}

			if (type == typeof (string))
			{
				return (T)(object)configValue;
			}

			throw new ArgumentException("Unrecognized type passed to GetConfig: " + typeof(T).Name);
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

		/// <summary>
		/// Sets a configuration value.
		/// </summary>
		/// <typeparam name="T">The type of configuration value. (bool, string, int, double)</typeparam>
		/// <param name="configName">The configuration key.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="connection">The connection to save to.</param>
		public static void Set<T>(string configName, T value, SQLiteConnection connection = null)
		{
			if (connection == null)
			{
				connection = Database.ThreadLocalConnection;
			}

			string configValue = GetConfigValue(value);
			SetInternal(configName, configValue, connection);
		}

		/// <summary>
		/// Sets a configuration value to a legacy DB (pre version 35). Used only when upgrading the DB before that point.
		/// </summary>
		/// <typeparam name="T">The type of configuration value. (bool, string, int, double)</typeparam>
		/// <param name="configName">The configuration key.</param>
		/// <param name="value">The value to set.</param>
		/// <param name="connection">The connection to save to.</param>
		public static void SetLegacy<T>(string configName, T value, SQLiteConnection connection = null)
		{
			if (connection == null)
			{
				connection = Database.ThreadLocalConnection;
			}

			string configValue = GetConfigValue(value);
			SetInternalLegacy(configName, configValue, connection);
		}

		private static string GetConfigValue<T>(T value)
		{
			if (value is double)
			{
				double typedValue = (double)Convert.ChangeType(value, typeof(double));
				return typedValue.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is int)
			{
				int typedValue = (int)Convert.ChangeType(value, typeof(int));
				return typedValue.ToString(CultureInfo.InvariantCulture);
			}
			else if (value is string || value is bool)
			{
				return value.ToString();
			}
			else if (value == null)
			{
				return null;
			}
			else
			{
				throw new ArgumentException("Unrecognized type passed to Set: " + typeof(T).Name);
			}
		}

		private static void SetInternal(string configName, string configValue, SQLiteConnection connection)
		{
			using (var settingsInsert = new SQLiteCommand(connection))
			{
				settingsInsert.CommandText = "REPLACE INTO settings (name, value) VALUES (?, ?)";
				settingsInsert.Parameters.Add("name", DbType.String).Value = configName;
				settingsInsert.Parameters.Add("value", DbType.String).Value = configValue;

				settingsInsert.ExecuteNonQuery();
			}
		}

		public static void SetInternalLegacy(string configName, string configValue, SQLiteConnection connection)
		{
			var command = new SQLiteCommand("UPDATE settings SET value = ? WHERE name = ?", connection);
			command.Parameters.Add("value", DbType.String).Value = configValue;
			command.Parameters.Add("name", DbType.String).Value = configName;

			if (command.ExecuteNonQuery() == 0)
			{
				// If the setting did not exist, add it
				using (var settingsInsert = new SQLiteCommand(connection))
				{
					settingsInsert.CommandText = "INSERT INTO settings (name, value) VALUES (?, ?)";
					settingsInsert.Parameters.Add("name", DbType.String).Value = configName;
					settingsInsert.Parameters.Add("value", DbType.String).Value = configValue;

					settingsInsert.ExecuteNonQuery();
				}
			}
		}
	}
}
