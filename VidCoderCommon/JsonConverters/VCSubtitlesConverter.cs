using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;

namespace VidCoderCommon.JsonConverters;

public class VCSubtitlesConverter : JsonConverter<VCSubtitles>
{
	public override VCSubtitles Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// Don't pass in options when recursively calling Deserialize.
		VCSubtitles subtitles = JsonSerializer.Deserialize<VCSubtitles>(ref reader, JsonOptions.Plain);

		// Upgrade out-of-date fields
#pragma warning disable 618
		if (subtitles.SrtSubtitles != null && subtitles.FileSubtitles == null)
		{
			subtitles.FileSubtitles = subtitles.SrtSubtitles;
		}

		subtitles.SrtSubtitles = null;
#pragma warning restore 618

		return subtitles;
	}

	public override void Write(Utf8JsonWriter writer, VCSubtitles value, JsonSerializerOptions options)
	{
		// Don't pass in options when recursively calling Serialize.
		JsonSerializer.Serialize(writer, value, JsonOptions.Plain);
	}
}
