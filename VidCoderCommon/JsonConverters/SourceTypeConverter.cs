using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VidCoderCommon.Model;

namespace VidCoderCommon.JsonConverters
{
	public class SourceTypeConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var sourceType = (SourceType)value;
			writer.WriteValue(sourceType.ToString());
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return ParseSourceTypeString((string)reader.Value);
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(SourceType);
		}

		public static SourceType ParseSourceTypeString(string sourceTypeString)
		{
			switch (sourceTypeString)
			{
				case "VideoFolder":
					return SourceType.DiscVideoFolder;
				case "Dvd":
					return SourceType.Disc;
				default:
					return (SourceType)Enum.Parse(typeof(SourceType), sourceTypeString);
			}
		}
	}
}
