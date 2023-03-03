using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VidCoderCommon.JsonConverters;
using VidCoderCommon.Model;

namespace VidCoderCommon.Utilities;

public static class JsonOptions
{
	static JsonOptions()
	{
		withUpgraders = CreateDefaultOptions();
		withUpgraders.Converters.Add(new VCJobConverter());
		withUpgraders.Converters.Add(new VCSubtitlesConverter());

		plainOptions = CreateDefaultOptions();
	}

	private static JsonSerializerOptions CreateDefaultOptions()
	{
		var defaultOptions = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = true
		};
		defaultOptions.Converters.Add(new VersionConverter());
		defaultOptions.Converters.Add(new TimeSpanConverter());
		defaultOptions.Converters.Add(new JsonStringEnumConverter());

		return defaultOptions;
	}

	private static JsonSerializerOptions withUpgraders;
	public static JsonSerializerOptions WithUpgraders => withUpgraders;

	private static JsonSerializerOptions plainOptions;
	public static JsonSerializerOptions Plain => plainOptions;
}
