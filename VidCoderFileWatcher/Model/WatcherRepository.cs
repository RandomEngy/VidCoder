using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;

namespace VidCoderFileWatcher.Model
{
	public class WatcherRepository
	{
		public static List<StrippedPicker> GetPickerList()
		{
			var result = new List<StrippedPicker>();

			using (var selectPickersCommand = new SQLiteCommand("SELECT * FROM pickersJson", WatcherDatabase.Connection))
			using (SQLiteDataReader reader = selectPickersCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					string pickerJson = reader.GetString("json");
					StrippedPicker? picker = JsonSerializer.Deserialize<StrippedPicker>(pickerJson);

					if (picker != null)
					{
						result.Add(picker);
					}
				}
			}

			return result;
		}
	}
}
