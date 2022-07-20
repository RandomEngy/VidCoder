using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VidCoderCommon.Extensions;

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
	}
}
