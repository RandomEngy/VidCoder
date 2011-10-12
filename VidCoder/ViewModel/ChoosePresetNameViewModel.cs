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

		private string presetName;
		public string PresetName
		{
			get
			{
				return this.presetName;
			}

			set
			{
				this.presetName = value;
				this.RaisePropertyChanged("PresetName");
				this.AcceptCommand.RaiseCanExecuteChanged();
			}
		}
	}
}
