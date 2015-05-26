using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using System.Globalization;
	using System.ServiceModel.Channels;
	using System.Threading;
	using System.Windows;
	using Resources;
	using Services;

	public static class Database
	{
		private const string ConfigDatabaseFileWithoutExtension = "VidCoder";
		private const string ConfigDatabaseFileExtension = ".sqlite";
		private const string ConfigDatabaseFile = ConfigDatabaseFileWithoutExtension + ConfigDatabaseFileExtension;

		private static SQLiteConnection connection;

		private static ThreadLocal<SQLiteConnection> threadLocalConnection = new ThreadLocal<SQLiteConnection>();

		private static long mainThreadId;

		private static Lazy<string> lazyDatabaseFile = new Lazy<string>(GetDatabaseFilePath); 

		static Database()
		{
			mainThreadId = Thread.CurrentThread.ManagedThreadId;

			int databaseVersion = DatabaseConfig.Version;
			if (databaseVersion > Utilities.CurrentDatabaseVersion)
			{
				string messageLine1 = string.Format(
					CultureInfo.CurrentCulture, 
					MainRes.RenameDatabaseFileLine1,
					databaseVersion,
					Utilities.CurrentVersion,
					Utilities.CurrentDatabaseVersion);

				string messageLine2 = MainRes.RenameDatabaseFileLine2;

				string message = string.Format(
					CultureInfo.CurrentCulture,
					"{0}{1}{1}{2}",
					messageLine1,
					Environment.NewLine,
					messageLine2);

				var messageService = Ioc.Container.GetInstance<IMessageBoxService>();
				messageService.Show(message, MainRes.IncompatibleDatabaseFileTitle, MessageBoxButton.YesNo);
				if (messageService.Show(
					message,
					MainRes.IncompatibleDatabaseFileTitle,
					MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					Connection.Close();

					try
					{
						string newFileName = ConfigDatabaseFileWithoutExtension + "-v" + databaseVersion + ConfigDatabaseFileExtension;
						string newFilePath = FileUtilities.CreateUniqueFileName(newFileName, Utilities.AppFolder, new HashSet<string>());

						File.Move(NonPortableDatabaseFile, newFilePath);
						connection = null;
						databaseVersion = DatabaseConfig.Version;
					}
					catch (IOException)
					{
						HandleCriticalFileError();
					}
					catch (UnauthorizedAccessException)
					{
						HandleCriticalFileError();
					}
				}
				else
				{
					Environment.Exit(0);
				}
			}

			if (databaseVersion == Utilities.CurrentDatabaseVersion)
			{
				return;
			}

			using (SQLiteTransaction transaction = Connection.BeginTransaction())
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

					Presets.SavePresets(presetXmlList, Connection);

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
					ExecuteNonQuery(
						"CREATE TABLE workerLogs (" +
						"workerGuid TEXT, " +
						"message TEXT, " +
						"level INTEGER, " +
						"time TEXT)", connection);
				}

                if (databaseVersion < 27)
                {
                    ExecuteNonQuery("CREATE TABLE pickersXml (" +
                        "xml TEXT)", connection);

                    Config.Initialize(connection);

                    // If the user has chosen some auto audio or subtitle picker options, migrate them to a new picker
                    if (CustomConfig.AutoAudio != AudioSelectionMode.Disabled || CustomConfig.AutoSubtitle != SubtitleSelectionMode.Disabled)
                    {
                        using (var pickerInsertCommand = new SQLiteCommand("INSERT INTO pickersXml (xml) VALUES (?)", connection))
                        {
                            var pickerParameter = new SQLiteParameter();
                            pickerInsertCommand.Parameters.Add(pickerParameter);

                            var convertedPicker = new Picker
                            {
                                Name = string.Format(MainRes.PickerNameTemplate, 1),
                                AudioSelectionMode = CustomConfig.AutoAudio,
                                AudioLanguageCode = Config.AudioLanguageCode,
                                AudioLanguageAll = Config.AutoAudioAll,
                                SubtitleSelectionMode = CustomConfig.AutoSubtitle,
                                SubtitleForeignBurnIn = Config.AutoSubtitleBurnIn,
                                SubtitleLanguageCode = Config.SubtitleLanguageCode,
                                SubtitleLanguageAll = Config.AutoSubtitleAll,
                                SubtitleLanguageBurnIn = Config.AutoSubtitleLanguageBurnIn,
                                SubtitleLanguageDefault = Config.AutoSubtitleLanguageDefault,
                                SubtitleLanguageOnlyIfDifferent = Config.AutoSubtitleOnlyIfDifferent
                            };

                            pickerParameter.Value = PickerStorage.SerializePicker(convertedPicker);
                            pickerInsertCommand.ExecuteNonQuery();
                        }

                        Config.LastPickerIndex = 1;
                    }
                }

				SetDatabaseVersionToLatest();
				transaction.Commit();
			}
		}

		private static void HandleCriticalFileError()
		{
			var messageService = Ioc.Container.GetInstance<IMessageBoxService>();

			messageService.Show(CommonRes.FileFailureErrorMessage, CommonRes.FileFailureErrorTitle, MessageBoxButton.OK);
			Environment.Exit(1);
		}

		private static void SetDatabaseVersionToLatest()
		{
			DatabaseConfig.SetConfigValue("Version", Utilities.CurrentDatabaseVersion, Database.Connection);
		}

		/// <summary>
		/// Gets the database file path.
		/// </summary>
		public static string DatabaseFile
		{
			get { return lazyDatabaseFile.Value; }
		}

		private static string GetDatabaseFilePath()
		{
			if (Utilities.IsPortable)
			{
				string portableExeFolder = GetPortableExeFolder();
				if (FileUtilities.HasWriteAccessOnFolder(portableExeFolder))
				{
					// Portable location for database is beside the portable exe.
					string portableDatabasePath = Path.Combine(portableExeFolder, ConfigDatabaseFile);
					if (File.Exists(portableDatabasePath))
					{
						// Use portable location for database file if it exists
						return portableDatabasePath;
					}

					string appDataDatabaseFile = NonPortableDatabaseFile;
					if (File.Exists(appDataDatabaseFile))
					{
						// If database file does exist in appdata, use it.
						return appDataDatabaseFile;
					}
					else
					{
						// If no file could be found, use the portable location.
						return portableDatabasePath;
					}
				}
				else
				{
					// Fall back to non-portable path if we don't have write access to the folder.
					return NonPortableDatabaseFile;
				}
			}
			else
			{
				return NonPortableDatabaseFile;
			}
		}

		/// <summary>
		/// Gets the folder that contains the portable executable that launched us.
		/// </summary>
		/// <returns></returns>
		private static string GetPortableExeFolder()
		{
			if (!Utilities.IsPortable)
			{
				throw new InvalidOperationException("Called GetPortableExeFolder on a non-portable install.");
			}

			Process parentProcess = ParentProcessUtilities.GetParentProcess();
			return Path.GetDirectoryName(parentProcess.MainModule.FileName);
		}

		/// <summary>
		/// Gets the database file path for a non-portable version. This may be in
		/// the standard %appdata% folder or it may be in a user-specified directory.
		/// </summary>
		public static string NonPortableDatabaseFile
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
				if (Utilities.Beta && Directory.Exists(Utilities.GetAppFolder(beta: false)))
				{
					// In beta mode if we don't have the appdata folder copy the stable appdata folder
					FileUtilities.CopyDirectory(
						Utilities.GetAppFolder(beta: false),
						Utilities.GetAppFolder(beta: true));
				}
				else
				{
					Directory.CreateDirectory(Utilities.AppFolder);
				}
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
			ExecuteNonQuery("CREATE TABLE presetsXml (" +
				"xml TEXT)", connection);

            ExecuteNonQuery("CREATE TABLE pickersXml (" +
                "xml TEXT)", connection);

			ExecuteNonQuery(
				"CREATE TABLE settings (" +
				"name TEXT, " +
				"value TEXT)", connection);

			ExecuteNonQuery(
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
