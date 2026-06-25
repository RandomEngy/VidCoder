using System;
using System.Reactive.Linq;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services;

/// <summary>
/// Holds session-scoped temporary encode setting overrides and manages their lifecycle.
/// </summary>
public class EncodeSettingOverrideService : ReactiveObject
{
	private VCCropping cropping;

	public EncodeSettingOverrideService()
	{
		this.RegisterClearListeners();
	}

	public event EventHandler OverridesCleared;

	public bool HasCroppingOverride { get; private set; }

	public void SetCropping(VCCropping cropping)
	{
		this.cropping = new VCCropping(cropping);
		this.HasCroppingOverride = true;
		this.RaisePropertyChanged(nameof(this.HasCroppingOverride));
	}

	public VCCropping GetCroppingOrNull()
	{
		return this.HasCroppingOverride ? this.cropping : null;
	}

	public VCJobEncodeSettingOverrides ToJobOverrides(VCProfile profile)
	{
		if (!this.HasCroppingOverride)
		{
			return null;
		}

		if (profile != null &&
			profile.CroppingType != VCCroppingType.Automatic &&
			profile.CroppingType != VCCroppingType.Loose)
		{
			return null;
		}

		return new VCJobEncodeSettingOverrides
		{
			Cropping = new VCCropping(this.cropping),
		};
	}

	private void RegisterClearListeners()
	{
		PresetsService presetsService = StaticResolver.Resolve<PresetsService>();
		MainViewModel mainViewModel = StaticResolver.Resolve<MainViewModel>();

		presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
			.Skip(1)
			.Subscribe(_ => this.ClearAll());

		mainViewModel.WhenAnyValue(x => x.SourcePath)
			.Skip(1)
			.Subscribe(_ => this.ClearAll());

		mainViewModel.WhenAnyValue(x => x.SelectedTitle)
			.Skip(1)
			.Subscribe(_ => this.ClearAll());

		presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.CroppingType)
			.Skip(1)
			.Subscribe(croppingType =>
			{
				if (croppingType != VCCroppingType.Automatic && croppingType != VCCroppingType.Loose)
				{
					this.ClearCropping();
				}
			});

		presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.Rotation)
			.Skip(1)
			.Subscribe(_ => this.ClearCropping());

		presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.FlipHorizontal)
			.Skip(1)
			.Subscribe(_ => this.ClearCropping());

		presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.FlipVertical)
			.Skip(1)
			.Subscribe(_ => this.ClearCropping());
	}

	private void ClearAll()
	{
		bool hadAny = this.HasCroppingOverride;
		this.ClearCroppingInternal();

		if (hadAny)
		{
			this.OverridesCleared?.Invoke(this, EventArgs.Empty);
		}
	}

	public void ClearCropping()
	{
		if (!this.HasCroppingOverride)
		{
			return;
		}

		this.ClearCroppingInternal();
		this.OverridesCleared?.Invoke(this, EventArgs.Empty);
	}

	private void ClearCroppingInternal()
	{
		this.cropping = null;
		this.HasCroppingOverride = false;
		this.RaisePropertyChanged(nameof(this.HasCroppingOverride));
	}
}
