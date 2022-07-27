using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VidCoderCommon.Extensions;
using static VidCoderCommon.Model.CommonDatabase;

namespace VidCoderCommon.Model
{
	public static class WatcherStorage
	{
		public static List<WatchedFolder> GetWatchedFolders(SQLiteConnection connection)
		{
			var result = new List<WatchedFolder>();

			using (var selectWatchedFoldersCommand = new SQLiteCommand("SELECT * FROM watchedFolders", connection))
			using (SQLiteDataReader reader = selectWatchedFoldersCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					string watchedFolderJson = reader.GetString("json");
					WatchedFolder? watchedFolder = JsonSerializer.Deserialize<WatchedFolder>(watchedFolderJson);

					if (watchedFolder != null)
					{
						result.Add(watchedFolder);
					}
				}
			}

			return result;
		}

		public static long GetWatchedFolderCount(SQLiteConnection connection)
		{
			using (var getWatchedFolderCountCommand = new SQLiteCommand("SELECT COUNT(*) FROM watchedFolders", connection))
			{
				return (long)getWatchedFolderCountCommand.ExecuteScalar();
			}
		}

		public static void SaveWatchedFolders(SQLiteConnection connection, List<WatchedFolder> watchedFolders)
		{
			using (var transaction = connection.BeginTransaction())
			{
				ExecuteNonQuery("DELETE FROM watchedFolders", connection);

				var insertCommand = new SQLiteCommand("INSERT INTO watchedFolders (json) VALUES (?)", connection);
				SQLiteParameter insertJsonParam = insertCommand.Parameters.Add("json", DbType.String);

				foreach (WatchedFolder watchedFolder in watchedFolders)
				{
					insertJsonParam.Value = JsonSerializer.Serialize(watchedFolder);
					insertCommand.ExecuteNonQuery();
				}

				transaction.Commit();
			}
		}
	}
}
