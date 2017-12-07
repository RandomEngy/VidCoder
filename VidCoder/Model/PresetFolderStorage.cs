using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Model
{
	public static class PresetFolderStorage
	{
		public static IList<PresetFolder> PresetFolders
		{
			get
			{
				var result = new List<PresetFolder>();

				var selectPresetsCommand = new SQLiteCommand("SELECT * FROM presetFolders", Database.Connection);
				using (SQLiteDataReader reader = selectPresetsCommand.ExecuteReader())
				{
					while (reader.Read())
					{
						long id = reader.GetInt64("id");
						string name = reader.GetString("name");
						long parentId = reader.GetInt64("parentId");
						result.Add(new PresetFolder { Id = id, Name = name, ParentId = parentId });
					}
				}

				return result;
			}
		}

		public static void RenameFolder(long id, string newName)
		{
			SQLiteConnection connection = Database.ThreadLocalConnection;
			var updateCommand = new SQLiteCommand("UPDATE presetFolders SET name = @name WHERE id = @id", connection);
			updateCommand.Parameters.AddWithValue("@name", newName);
			updateCommand.Parameters.AddWithValue("@id", id);
			updateCommand.ExecuteNonQuery();
		}

		public static void MoveFolder(long id, long newParent)
		{
			SQLiteConnection connection = Database.ThreadLocalConnection;
			var updateCommand = new SQLiteCommand("UPDATE presetFolders SET parentId = @parentId WHERE id = @id", connection);
			updateCommand.Parameters.AddWithValue("@parentId", newParent);
			updateCommand.Parameters.AddWithValue("@id", id);
			updateCommand.ExecuteNonQuery();
		}

		public static PresetFolder AddFolder(string name, long parentId)
		{
			SQLiteConnection connection = Database.ThreadLocalConnection;

			var insertCommand = new SQLiteCommand("INSERT INTO presetFolders (name, parentId) VALUES (@name, @parentId)", connection);
			insertCommand.Parameters.AddWithValue("@name", name);
			insertCommand.Parameters.AddWithValue("@parentId", parentId);
			insertCommand.ExecuteNonQuery();

			return new PresetFolder { Id = connection.LastInsertRowId, Name = name, ParentId = parentId };
		}

		public static void RemoveFolder(long id)
		{
			SQLiteConnection connection = Database.ThreadLocalConnection;
			var deleteCommand = new SQLiteCommand("DELETE FROM presetFolders WHERE id = @id", connection);
			deleteCommand.Parameters.AddWithValue("@id", id);
			deleteCommand.ExecuteNonQuery();
		}
	}
}
