using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using HandBrake.Interop.Model.Encoding;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	using System.Resources;
	using LocalResources;

	public class PresetViewModel : ViewModelBase
	{
		private static ResourceManager manager = new ResourceManager(typeof (MainRes));

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
				if (!this.preset.IsBuiltIn)
				{
					return this.preset.Name;
				}

				string displayName = manager.GetString("Preset_" + this.preset.Name);
				if (displayName == null)
				{
					return this.preset.Name;
				}

				return displayName;
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
