using System.Collections.Generic;
using System.Reactive.Linq;
using HandBrake.ApplicationServices.Interop;
using ReactiveUI;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class LevelChoiceViewModel : ReactiveObject
	{
		private MainViewModel main = Ioc.Get<MainViewModel>();
		private PresetsService presetsService = Ioc.Get<PresetsService>();
		private OutputSizeService outputSizeService = Ioc.Get<OutputSizeService>();

		public LevelChoiceViewModel(string value)
		{
			this.Value = value;
			this.Display = value;

			this.RegisterIsCompatible();
		}

		public LevelChoiceViewModel(string value, string display)
		{
			this.Value = value;
			this.Display = display;

			this.RegisterIsCompatible();
		}

		private void RegisterIsCompatible()
		{
			Observable.CombineLatest(
				this.main.WhenAnyValue(x => x.HasVideoSource),
				this.main.WhenAnyValue(x => x.SelectedTitle.FrameRate),
				this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.Framerate),
				this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.VideoOptions),
				this.outputSizeService.WhenAnyValue(x => x.Size),
				(hasVideoSource, titleFramerate, profileFramerate, videoOptions, outputSize) =>
				{
					// Checking H264 level is no longer possible in HB. It may come back in the future.
					return true;

					////if (this.Value == null || !hasVideoSource || outputSize == null || outputSize.Width == 0 || outputSize.Height == 0)
					////{
					////	return true;
					////}

					////int fpsNumerator;
					////int fpsDenominator;

					////if (profileFramerate == 0)
					////{
					////	fpsNumerator = titleFramerate.Num;
					////	fpsDenominator = titleFramerate.Den;
					////}
					////else
					////{
					////	fpsNumerator = 27000000;
					////	fpsDenominator = HandBrakeUnitConversionHelpers.FramerateToVrate(profileFramerate);
					////}

					////bool interlaced = false;
					////bool fakeInterlaced = false;

					////Dictionary<string, string> advancedOptions = AdvancedOptionsParsing.ParseOptions(videoOptions);
					////if (advancedOptions.ContainsKey("interlaced") && advancedOptions["interlaced"] == "1" ||
					////	advancedOptions.ContainsKey("tff") && advancedOptions["tff"] == "1" ||
					////	advancedOptions.ContainsKey("bff") && advancedOptions["bff"] == "1")
					////{
					////	interlaced = true;
					////}

					////if (advancedOptions.ContainsKey("fake-interlaced") && advancedOptions["fake-interlaced"] == "1")
					////{
					////	fakeInterlaced = true;
					////}

					////return HandBrakeUtils.IsH264LevelValid(
					////	this.Value,
					////	outputSize.Width,
					////	outputSize.Height,
					////	fpsNumerator,
					////	fpsDenominator,
					////	interlaced,
					////	fakeInterlaced);
				}).ToProperty(this, x => x.IsCompatible, out this.isCompatible);
		}

		public string Value { get; set; }

		public string Display { get; set; }

		private ObservableAsPropertyHelper<bool> isCompatible;
		public bool IsCompatible => this.isCompatible.Value;

		public override string ToString()
	    {
	        return this.Display;
	    }
	}
}
