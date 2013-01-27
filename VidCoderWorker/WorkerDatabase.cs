using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.Data.SQLite;
	using System.IO;

	public class WorkerDatabase
	{
		private const string ConfigDatabaseFile = "VidCoder.sqlite";
		private static SQLiteConnection connection;

		public static string DatabaseFile
		{
			get
			{
				string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidCoder");

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
