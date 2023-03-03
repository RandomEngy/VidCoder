using ReactiveUI;
using System.Reactive.Concurrency;
using VidCoder.Extensions;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel;

public class PresetViewModel : ReactiveObject
{
	public PresetViewModel(Preset preset)
	{
		this.Preset = preset;

		this.Preset.WhenAnyValue(
			x => x.Name,
			x => x.IsBuiltIn,
			(name, isBuiltIn) =>
			{
				return PresetExtensions.GetDisplayName(name, isBuiltIn);
			})
			.ToProperty(this, x => x.DisplayName, out this.displayName, scheduler: Scheduler.Immediate);

		this.WhenAnyValue(
			x => x.DisplayName,
			x => x.Preset.IsModified,
			(displayNameParameter, isModified) =>
			{
				string suffix = isModified ? " *" : string.Empty;
				return displayNameParameter + suffix;
			})
			.ToProperty(this, x => x.DisplayNameWithStar, out this.displayNameWithStar, scheduler: Scheduler.Immediate);
	}

	public Preset Preset { get; }

	// Used only to help TreeViewModel. The real selected master property is on PresetsService
	private bool isSelected;
	public bool IsSelected
	{
		get { return this.isSelected; }
		set { this.RaiseAndSetIfChanged(ref this.isSelected, value); }
	}

	public PresetFolderViewModel Parent { get; set; }

	public VCProfile OriginalProfile { get; set; }

	private ObservableAsPropertyHelper<string> displayName;
	public string DisplayName => this.displayName.Value;

	private ObservableAsPropertyHelper<string> displayNameWithStar;
	public string DisplayNameWithStar => this.displayNameWithStar.Value;

	public override string ToString()
	{
		return this.DisplayName;
	}

	public PresetViewModel Clone(PresetFolderViewModel parent)
	{
		return new PresetViewModel(this.Preset)
		{
			Parent = parent,
			OriginalProfile = this.OriginalProfile,
		};
	}
}
