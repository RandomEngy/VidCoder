using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using VidCoder.Model;
using VidCoder.Properties;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class PresetImportExport : IPresetImportExport
	{
		private IFileService fileService;
		private IMessageBoxService messageBoxService;
		private MainViewModel mainViewModel;

		public PresetImportExport(IFileService fileService, IMessageBoxService messageBoxService, MainViewModel mainViewModel)
		{
			this.fileService = fileService;
			this.messageBoxService = messageBoxService;
			this.mainViewModel = mainViewModel;
		}

		public void ImportPreset(string presetFile)
		{
			Preset preset = Presets.LoadPresetFile(presetFile);
			if (preset == null || string.IsNullOrWhiteSpace(preset.Name))
			{
				this.messageBoxService.Show("Could not import preset. Format is unrecognized.", "Import Error", System.Windows.MessageBoxButton.OK);
				return;
			}

			preset.IsBuiltIn = false;
			preset.IsModified = false;

			List<Preset> existingPresets = Presets.UserPresets;
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

			this.messageBoxService.Show("Successfully imported preset " + preset.Name + ".", "Success", System.Windows.MessageBoxButton.OK);

			this.mainViewModel.AddPreset(preset);
		}

		public void ExportPreset(Preset preset)
		{
			string initialFileName = preset.Name;
			if (preset.IsModified)
			{
				initialFileName += "_Modified";
			}

			string exportFileName = this.fileService.GetFileNameSave(
				Settings.Default.LastPresetExportFolder,
				Utilities.CleanFileName(initialFileName + ".xml"),
				"xml",
				"XML Files|*.xml");
			if (exportFileName != null)
			{
				Settings.Default.LastPresetExportFolder = Path.GetDirectoryName(exportFileName);
				Settings.Default.Save();

				if(Presets.SavePresetToFile(preset, exportFileName))
				{
					this.messageBoxService.Show(
						"Successfully exported preset to " + exportFileName,
						"Success",
						System.Windows.MessageBoxButton.OK);
				}
			}
		}
	}
}
