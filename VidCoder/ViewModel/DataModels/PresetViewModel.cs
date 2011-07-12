using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model.Encoding;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	public class PresetViewModel : ViewModelBase
	{
		private Preset preset;

		public PresetViewModel(Preset preset)
		{
			this.preset = preset;
		}

		public Preset Preset
		{
			get
			{
				return this.preset;
			}
		}

		public EncodingProfile OriginalProfile { get; set; }

		public bool IsModified
		{
			get
			{
				return this.preset.IsModified;
			}
		}

		public string PresetName
		{
			get
			{
				return this.preset.Name;
			}
		}

		public bool IsBuiltIn
		{
			get
			{
				return this.preset.IsBuiltIn;
			}
		}

		public void RefreshView()
		{
			this.NotifyPropertyChanged("IsModified");
			this.NotifyPropertyChanged("PresetName");
			this.NotifyPropertyChanged("IsBuiltIn");
		}
	}
}
