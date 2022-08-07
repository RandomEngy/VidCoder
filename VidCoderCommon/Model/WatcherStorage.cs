﻿using System;
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

		public static void SaveWatchedFolders(SQLiteConnection connection, IEnumerable<WatchedFolder> watchedFolders)
		{
			using (var transaction = connection.BeginTransaction())
			{
				ExecuteNonQuery("DELETE FROM watchedFolders", connection);

				var insertCommand = new SQLiteCommand("INSERT INTO watchedFolders (json) VALUES (@json)", connection);
				SQLiteParameter insertJsonParam = insertCommand.Parameters.Add("@json", DbType.String);

				foreach (WatchedFolder watchedFolder in watchedFolders)
				{
					insertJsonParam.Value = JsonSerializer.Serialize(watchedFolder);
					insertCommand.ExecuteNonQuery();
				}

				transaction.Commit();
			}
		}

		public static Dictionary<string, WatchedFile> GetWatchedFiles(SQLiteConnection connection)
		{
			return GetFileEntries(connection, "SELECT * FROM watchedFiles");
		}

		public static Dictionary<string, WatchedFile> GetPlannedFiles(SQLiteConnection connection)
		{
			return GetFileEntries(connection, "SELECT * FROM watchedFiles WHERE status = 'Planned'");
		}

		private static Dictionary<string, WatchedFile> GetFileEntries(SQLiteConnection connection, string selectStatement)
		{
			var result = new Dictionary<string, WatchedFile>(StringComparer.OrdinalIgnoreCase);

			using (var selectWatchedFoldersCommand = new SQLiteCommand(selectStatement, connection))
			using (SQLiteDataReader reader = selectWatchedFoldersCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					string path = reader.GetString("path");
					string status = reader.GetString("status");
					string reason = reader.IsDBNull("reason") ? null : reader.GetString("reason");

					var watchedFile = new WatchedFile
					{
						Path = path,
						Status = Enum.Parse<WatchedFileStatus>(status)
					};

					if (reason != null)
					{
						watchedFile.Reason = Enum.Parse<WatchedFileStatusReason>(reason);
					}

					result.Add(path, watchedFile);
				}
			}

			return result;
		}

		public static void RemoveEntries(SQLiteConnection connection, IEnumerable<string> paths)
		{
			using (var transaction = connection.BeginTransaction())
			{
				using (var removeCommand = new SQLiteCommand("DELETE FROM watchedFiles WHERE path = @path", connection))
				{
					SQLiteParameter pathParameter = removeCommand.Parameters.Add("@path", DbType.String);

					foreach (string path in paths)
					{
						pathParameter.Value = path;
						removeCommand.ExecuteNonQuery();
					}
				}

				transaction.Commit();
			}
		}

		public static void AddEntries(SQLiteConnection connection, IEnumerable<string> paths)
		{
			using (var transaction = connection.BeginTransaction())
			{
				using (var addCommand = new SQLiteCommand("INSERT INTO watchedFiles (path, status) VALUES (@path, @status)", connection))
				{
					SQLiteParameter pathParameter = addCommand.Parameters.Add("@path", DbType.String);
					addCommand.Parameters.AddWithValue("@status", WatchedFileStatus.Planned.ToString());

					foreach (string path in paths)
					{
						pathParameter.Value = path;
						addCommand.ExecuteNonQuery();
					}
				}

				transaction.Commit();
			}
		}

		public static void UpdateEntryStatus(SQLiteConnection connection, string path, WatchedFileStatus status)
		{
			using (var updateCommand = new SQLiteCommand("UPDATE watchedFiles SET status = @status WHERE path = @path", connection))
			{
				updateCommand.Parameters.AddWithValue("@status", status);
				updateCommand.Parameters.AddWithValue("@path", path);
				updateCommand.ExecuteNonQuery();
			}
		}
	}
}
