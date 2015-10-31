using ReactiveUI;
using VidCoder.Extensions;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class PresetViewModel : ReactiveObject
	{
		private Preset preset;

		public PresetViewModel(Preset preset)
		{
			this.preset = preset;

			this.preset.WhenAnyValue(
				x => x.Name,
				x => x.IsBuiltIn,
				(name, isBuiltIn) =>
				{
					return PresetExtensions.GetDisplayName(name, isBuiltIn);
				})
				.ToProperty(this, x => x.DisplayName, out this.displayName);

			this.WhenAnyValue(
				x => x.DisplayName,
				x => x.Preset.IsModified,
				(displayNameParameter, isModified) =>
				{
					string suffix = isModified ? " *" : string.Empty;
					return displayNameParameter + suffix;
				})
				.ToProperty(this, x => x.DisplayNameWithStar, out this.displayNameWithStar);
		}

		public Preset Preset
		{
			get
			{
				return this.preset;
			}
		}

		public VCProfile OriginalProfile { get; set; }

		private ObservableAsPropertyHelper<string> displayName;
		public string DisplayName => this.displayName.Value;

		private ObservableAsPropertyHelper<string> displayNameWithStar;
		public string DisplayNameWithStar => this.displayNameWithStar.Value;
	}
}
