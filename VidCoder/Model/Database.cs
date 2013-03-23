using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using System.Globalization;
	using System.Threading;

	public static class Database
	{
		private const string ConfigDatabaseFile = "VidCoder.sqlite";

		private static SQLiteConnection connection;

		private static ThreadLocal<SQLiteConnection> threadLocalConnection = new ThreadLocal<SQLiteConnection>();

		private static long mainThreadId;

		static Database()
		{
			mainThreadId = Thread.CurrentThread.ManagedThreadId;

			int databaseVersion = DatabaseConfig.Version;
			if (databaseVersion >= Utilities.CurrentDatabaseVersion)
			{
				return;
			}

			using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
			{
				// Update encoding profiles if we need to.
				if (databaseVersion < Utilities.LastUpdatedEncodingProfileDatabaseVersion)
				{
					// Upgrade encoding profiles on presets (encoder/mixdown changes)
					var presets = Presets.GetPresetListFromDb();

					foreach (Preset preset in presets)
					{
						Presets.UpgradeEncodingProfile(preset.EncodingProfile, databaseVersion);
					}

					var presetXmlList = presets.Select(Presets.SerializePreset).ToList();

					Presets.SavePresets(presetXmlList, Database.Connection);

					// Upgrade encoding profiles on old queue items.
					string jobsXml = Config.EncodeJobs2;
					if (!string.IsNullOrEmpty(jobsXml))
					{
						EncodeJobPersistGroup persistGroup = EncodeJobsPersist.LoadJobsXmlString(jobsXml);
						foreach (EncodeJobWithMetadata job in persistGroup.EncodeJobs)
						{
							Presets.UpgradeEncodingProfile(job.Job.EncodingProfile, databaseVersion);
						}

						Config.EncodeJobs2 = EncodeJobsPersist.SerializeJobs(persistGroup);
					}
				}

				// Update DB schema
				if (databaseVersion < 18)
				{
					Database.ExecuteNonQuery(
						"CREATE TABLE workerLogs (" +
						"workerGuid TEXT, " +
						"message TEXT, " +
						"level INTEGER, " +
						"time TEXT)", connection);
				}

				SetDatabaseVersionToLatest();
				transaction.Commit();
			}
		}

		private static void SetDatabaseVersionToLatest()
		{
			DatabaseConfig.SetConfigValue("Version", Utilities.CurrentDatabaseVersion, Database.Connection);
		}

		public static string DatabaseFile
		{
			get
			{
				string appDataFolder = Utilities.AppFolder;

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
				return connection ?? (connection = CreateConnection());
			}
		}

		public static SQLiteConnection ThreadLocalConnection
		{
			get
			{
				if (IsMainThread)
				{
					return Connection;
				}

				if (!threadLocalConnection.IsValueCreated)
				{
					threadLocalConnection.Value = Database.CreateConnection();
				}

				return threadLocalConnection.Value;
			}
		}

		public static bool IsMainThread
		{
			get
			{
				return Thread.CurrentThread.ManagedThreadId == mainThreadId;
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

				var settingsList = new Dictionary<string, string> {{"Version", Utilities.CurrentDatabaseVersion.ToString(CultureInfo.InvariantCulture)}};
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

			Database.ExecuteNonQuery(
				"CREATE TABLE workerLogs (" +
				"workerGuid TEXT, " +
				"message TEXT, " +
				"level INTEGER, " + 
				"time TEXT)", connection);
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
