using System;
using VidCoder.Model;

namespace VidCoder.Services
{
	public interface IPresetImportExport
	{
		void ImportPreset(string presetFile);
		void ExportPreset(Preset preset);
	}
}
