using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoderWorker
{
	using System.Data.SQLite;
	using System.Diagnostics;
	using System.IO;

	public static class WorkerLogger
	{
		public static void Log(string message, bool isError = false)
		{
			SQLiteConnection connection = WorkerDatabase.CreateConnection();

			try
			{
				using (var command = new SQLiteCommand(connection))
				{
					command.CommandText = "INSERT INTO workerLogs (workerGuid, message, level, time) VALUES (?, ?, ?, ?)";
					command.Parameters.AddWithValue("workerGuid", Program.PipeGuidString);
					command.Parameters.AddWithValue("message", message);
					command.Parameters.AddWithValue("level", isError ? 1 : 0);
					command.Parameters.AddWithValue("time", DateTimeOffset.UtcNow.ToString("o"));

					command.ExecuteNonQuery();
				}
			}
			finally
			{
				connection.Close();
			}
		}
	}
}
