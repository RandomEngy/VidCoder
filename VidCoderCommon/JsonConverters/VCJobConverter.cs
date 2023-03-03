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

public class VCJobConverter : JsonConverter<VCJob>
{
	public override VCJob Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// Don't pass in options when recursively calling Deserialize.
		VCJob job = JsonSerializer.Deserialize<VCJob>(ref reader, JsonOptions.Plain);

		// Upgrade out-of-date fields
#pragma warning disable 618
		if (job.OutputPath != null && job.FinalOutputPath == null)
		{
			job.FinalOutputPath = job.OutputPath;
		}

		if (job.ChosenAudioTracks != null && job.AudioTracks == null)
		{
			job.AudioTracks = job.ChosenAudioTracks.Select(t => new ChosenAudioTrack { TrackNumber = t }).ToList();
		}

		job.OutputPath = null;
		job.ChosenAudioTracks = null;
#pragma warning restore 618

		return job;
	}

	public override void Write(Utf8JsonWriter writer, VCJob value, JsonSerializerOptions options)
	{
		// Don't pass in options when recursively calling Serialize.
		JsonSerializer.Serialize(writer, value, JsonOptions.Plain);
	}
}
