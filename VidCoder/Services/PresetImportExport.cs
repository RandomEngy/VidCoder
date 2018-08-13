using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AnyContainer;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	using Resources;

	public class PresetImportExport : IPresetImportExport
	{
		private IFileService fileService;
		private IMessageBoxService messageBoxService;
		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();
		private IAppLogger logger;

		public PresetImportExport(IFileService fileService, IMessageBoxService messageBoxService, IAppLogger logger)
		{
			this.fileService = fileService;
			this.messageBoxService = messageBoxService;
			this.logger = logger;
		}

		public Preset ImportPreset(string presetFile)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(presetFile))
				{
					throw new ArgumentException("Preset file path is required.");
				}

				Preset preset = PresetStorage.LoadPresetFile(presetFile);
				if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
				{
					throw new ArgumentException("Preset file was invalid.");
				}

				preset.IsBuiltIn = false;
				preset.IsModified = false;

				List<Preset> existingPresets = PresetStorage.UserPresets;
				if (existingPresets.Count(existingPreset => existingPreset.Name == preset.Name) > 0)
				{
					string proposedName;

					for (int i = 2; i < 100; i++)
					{
						proposedName = preset.Name + " (" + i + ")";
						if (existingPresets.Count(existingPreset => existingPreset.Name == proposedName) == 0)
						{
							preset.Name = proposedName;
							break;
						}
					}
				}

				this.presetsService.AddPreset(preset);

				return preset;
			}
			catch (Exception exception)
			{
				this.logger.LogError("Preset import failed: " + exception.Message);
				throw;
			}
		}

		public void ExportPreset(Preset preset)
		{
			var exportPreset = new Preset
			    {
					EncodingProfile = preset.EncodingProfile.Clone(),
					IsBuiltIn = false,
					IsModified = false,
					IsQueue = false,
					Name = preset.Name
			    };

			string initialFileName = exportPreset.Name;
			if (preset.IsModified)
			{
				initialFileName += "_Modified";
			}

			string exportFileName = this.fileService.GetFileNameSave(
				Config.RememberPreviousFiles ? Config.LastPresetExportFolder : null,
				MainRes.ExportPresetFilePickerText,
				FileUtilities.CleanFileName(initialFileName + ".vjpreset"),
				"vjpreset",
				CommonRes.PresetFileFilter + "|*.vjpreset");
			if (exportFileName != null)
			{
				if (Config.RememberPreviousFiles)
				{
					Config.LastPresetExportFolder = Path.GetDirectoryName(exportFileName);
				}

				if (PresetStorage.SavePresetToFile(exportPreset, exportFileName))
				{
					this.messageBoxService.Show(
						string.Format(MainRes.PresetExportSuccessMessage, exportFileName),
						CommonRes.Success,
						System.Windows.MessageBoxButton.OK);
				}
			}
		}
	}
}
