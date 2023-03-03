using VidCoderCommon.Model;

namespace VidCoder.Services;

public interface IPresetImportExport
{
	Preset ImportPreset(string presetFile);
	void ExportPreset(Preset preset);
}
