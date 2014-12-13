using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using HandBrake.Interop.Model.Encoding;
using VidCoder.Model;
using VidCoder.Model.Encoding;

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

		public VCProfile OriginalProfile { get; set; }

		public bool IsModified
		{
			get
			{
				return this.preset.IsModified;
			}
		}

		public bool IsQueue
		{
			get
			{
				return this.preset.IsQueue;
			}
		}

		public string PresetName
		{
			get
			{
				return this.preset.Name;
			}
		}

		public string DisplayName
		{
			get
			{
				return this.preset.DisplayName;
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
			this.RaisePropertyChanged(() => this.IsModified);
			this.RaisePropertyChanged(() => this.PresetName);
			this.RaisePropertyChanged(() => this.DisplayName);
			this.RaisePropertyChanged(() => this.IsBuiltIn);
		}
	}
}
