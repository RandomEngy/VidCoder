using GalaSoft.MvvmLight;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoderCommon.Model;

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
				return this.preset.GetDisplayName();
			}
		}

		public string DisplayNameWithStar
		{
			get
			{
				string suffix = this.IsModified ? " *" : string.Empty;
				return this.preset.GetDisplayName() + suffix;
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
			this.RaisePropertyChanged(() => this.DisplayNameWithStar);
			this.RaisePropertyChanged(() => this.IsBuiltIn);
		}
	}
}
