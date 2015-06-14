using System.Resources;
using VidCoder.Resources;
using VidCoderCommon.Model;

namespace VidCoder.Extensions
{
	public static class PresetExtensions
	{
		private static ResourceManager manager = new ResourceManager(typeof(MainRes));

		public static string GetDisplayName(this Preset preset)
		{
			if (!preset.IsBuiltIn)
			{
				return preset.Name;
			}

			string displayName = manager.GetString("Preset_" + preset.Name);
			if (displayName == null)
			{
				return preset.Name;
			}

			return displayName;
		}
	}
}
