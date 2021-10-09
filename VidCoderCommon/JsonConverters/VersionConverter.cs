using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VidCoderCommon.JsonConverters
{
	public class VersionConverter : JsonConverter<Version>
	{
		public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			string stringValue = reader.GetString();
			if (stringValue == null)
			{
				return null;
			}

			return new Version(stringValue);
		}

		public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}
