using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Extensions;
using System.Text.Json;

namespace VidCoderFileWatcher.Model;

/// <summary>
/// SQLite database access for the watcher process.
/// This is a stripped down version that does not do any upgrades, backups or checking for portable file locations.
/// </summary>
public static class WatcherDatabase
{
	private static ThreadLocal<SQLiteConnection> threadLocalConnection = new(trackAllValues: true);

	public static SQLiteConnection? Connection
	{
		get
		{
			if (!threadLocalConnection.IsValueCreated)
			{
				SQLiteConnection? connection = CreateConnection();
				if (connection == null)
				{
					return null;
				}
				else
				{
					threadLocalConnection.Value = connection;
				}
			}

			return threadLocalConnection.Value;
		}
	}

	public static SQLiteConnection? CreateConnection()
	{
		if (!File.Exists(CommonDatabase.NonPortableDatabaseFile))
		{
			return null;
		}

		var connection = new SQLiteConnection("Data Source=" + CommonDatabase.NonPortableDatabaseFile);
		connection.Open();

		return connection;
	}
}
