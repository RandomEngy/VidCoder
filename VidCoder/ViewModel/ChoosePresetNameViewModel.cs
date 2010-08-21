using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	public class ChoosePresetNameViewModel : OkCancelDialogViewModel
	{
		private IEnumerable<PresetViewModel> existingPresets;

		public ChoosePresetNameViewModel(IEnumerable<PresetViewModel> existingPresets)
		{
			this.existingPresets = existingPresets;
		}

		public override bool CanClose
		{
			get
			{
				if (this.PresetName == null || this.PresetName.Trim().Length == 0)
				{
					return false;
				}

				foreach (PresetViewModel presetVM in this.existingPresets)
				{
					if ((!presetVM.IsModified || presetVM.IsBuiltIn) && presetVM.PresetName == this.PresetName)
					{
						return false;
					}
				}

				return true;
			}
		}

		public string PresetName { get; set; }
	}
}
