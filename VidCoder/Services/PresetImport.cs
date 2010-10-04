using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class PresetImport : IPresetImport
	{
		private IMessageBoxService messageBoxService;
		private MainViewModel mainViewModel;

		public PresetImport(IMessageBoxService messageBoxService, MainViewModel mainViewModel)
		{
			this.messageBoxService = messageBoxService;
			this.mainViewModel = mainViewModel;
		}

		public void ImportPreset(string presetFile)
		{
			Preset preset = Presets.LoadPreset(presetFile);
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
	}
}
