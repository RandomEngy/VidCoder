using System.Resources;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.Extensions;

public static class PresetExtensions
{
	private static ResourceManager manager = new(typeof(MainRes));

	public static string GetDisplayName(this Preset preset)
	{
		return GetDisplayName(preset.Name, preset.IsBuiltIn);
	}

	public static string GetDisplayName(string name, bool isBuiltIn)
	{
		if (!isBuiltIn)
		{
			return name;
		}

		string displayName = manager.GetString("Preset_" + name);
		if (displayName == null)
		{
			return name;
		}

		return displayName;
	}
}
