using System;
using System.Data;

namespace VidCoderCommon.Extensions
{
	public static class DataReaderExtensions
	{
		public static int GetInt32(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.GetInt32(fieldIndex);
		}

		public static long GetInt64(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.GetInt64(fieldIndex);
		}

		public static string GetString(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.GetString(fieldIndex);
		}

		public static bool GetBoolean(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.GetBoolean(fieldIndex);
		}

		public static DateTime GetDateTime(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.GetDateTime(fieldIndex);
		}

		public static double GetDouble(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.GetDouble(fieldIndex);
		}

		public static void GetBytes(this IDataReader reader, string fieldName, long fieldOffset, byte[] buffer, int bufferoffset, int length)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			reader.GetBytes(fieldIndex, fieldOffset, buffer, bufferoffset, length);
		}

		public static bool IsDBNull(this IDataReader reader, string fieldName)
		{
			int fieldIndex = reader.GetOrdinal(fieldName);
			return reader.IsDBNull(fieldIndex);
		}
	}
}
