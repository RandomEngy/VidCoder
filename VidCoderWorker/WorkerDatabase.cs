using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	public class WorkerDatabase
	{
		private const string ConfigDatabaseFile = "VidCoder.sqlite";
		private static SQLiteConnection connection;
		private static string settingsDirectory;

		static WorkerDatabase()
		{
			settingsDirectory = ConfigurationManager.AppSettings["SettingsDirectory"];
		}

		public static string DatabaseFile
		{
			get
			{
				return Path.Combine(AppFolder, ConfigDatabaseFile);
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

		public static SQLiteConnection CreateConnection()
		{
			var newConnection = new SQLiteConnection(ConnectionString);
			newConnection.Open();

			return newConnection;
		}

		public static string AppFolder
		{
			get
			{
				if (settingsDirectory != null)
				{
					return settingsDirectory;
				}

				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidCoder");
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
