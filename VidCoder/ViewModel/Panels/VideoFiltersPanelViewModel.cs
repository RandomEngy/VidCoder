using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Resources;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.HbLib;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class VideoFiltersPanelViewModel : PanelViewModel
	{
		private const string CustomDenoisePreset = "custom";
		private const int MinDeblock = 5;

		private static readonly ResourceManager EnumResourceManager = new ResourceManager(typeof(EnumsRes));

		public VideoFiltersPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();

			this.DetelecineChoices = this.GetFilterPresetChoices(hb_filter_ids.HB_FILTER_DETELECINE);

			this.DeinterlaceChoices = new List<ComboChoice<VCDeinterlace>>
			{
				new ComboChoice<VCDeinterlace>(VCDeinterlace.Off, CommonRes.Off),
				new ComboChoice<VCDeinterlace>(VCDeinterlace.Yadif, EnumsRes.Deinterlace_Yadif),
				new ComboChoice<VCDeinterlace>(VCDeinterlace.Decomb, EnumsRes.Deinterlace_Decomb),
			};

			this.CombDetectChoices = this.GetFilterPresetChoices(hb_filter_ids.HB_FILTER_COMB_DETECT, "CombDetect_");

            this.DenoiseChoices = new List<ComboChoice<VCDenoise>>
            {
                new ComboChoice<VCDenoise>(VCDenoise.Off, CommonRes.Off),
                new ComboChoice<VCDenoise>(VCDenoise.hqdn3d, EnumsRes.Denoise_HQDN3D),
                new ComboChoice<VCDenoise>(VCDenoise.NLMeans, EnumsRes.Denoise_NLMeans),
            };

			this.DenoiseTuneChoices = this.GetFilterTuneChoices(hb_filter_ids.HB_FILTER_NLMEANS, "DenoiseTune_");



			// CustomDetelecineVisible
			this.WhenAnyValue(x => x.Detelecine, detelecine =>
			{
				return detelecine == "custom";
			}).ToProperty(this, x => x.CustomDetelecineVisible, out this.customDetelecineVisible);

			// DeinterlacePresetChoices
			this.WhenAnyValue(x => x.DeinterlaceType)
				.Select(deinterlaceType =>
				{
					switch (deinterlaceType)
					{
						case VCDeinterlace.Off:
							return new List<ComboChoice>();
						case VCDeinterlace.Yadif:
							return this.GetFilterPresetChoices(hb_filter_ids.HB_FILTER_DEINTERLACE, "DeinterlacePreset_");
						case VCDeinterlace.Decomb:
							return this.GetFilterPresetChoices(hb_filter_ids.HB_FILTER_DECOMB, "DeinterlacePreset_");
						default:
							throw new ArgumentException("Unrecognized deinterlace type: " + deinterlaceType);
					}
				}).ToProperty(this, x => x.DeinterlacePresetChoices, out this.deinterlacePresetChoices);

			// DeinterlacePresetVisible
			this.WhenAnyValue(x => x.DeinterlaceType)
				.Select(deinterlaceType =>
				{
					return deinterlaceType != VCDeinterlace.Off;
				}).ToProperty(this, x => x.DeinterlacePresetVisible, out this.deinterlacePresetVisible);

			// CustomDeinterlaceVisible
			this.WhenAnyValue(x => x.DeinterlaceType, x => x.DeinterlacePreset, (deinterlaceType, deinterlacePreset) =>
			{
				return deinterlaceType != VCDeinterlace.Off && deinterlacePreset == "custom";
			}).ToProperty(this, x => x.CustomDeinterlaceVisible, out this.customDeinterlaceVisible);

			// CustomCombDetectVisible
			this.WhenAnyValue(x => x.CombDetect, combDetect =>
			{
				return combDetect == "custom";
			}).ToProperty(this, x => x.CustomCombDetectVisible, out this.customCombDetectVisible);

			// DenoisePresetVisible
			this.WhenAnyValue(x => x.DenoiseType, denoise =>
			{
				return denoise != VCDenoise.Off;
			}).ToProperty(this, x => x.DenoisePresetVisible, out this.denoisePresetVisible);

			// DenoisePresetChoices
			this.WhenAnyValue(x => x.DenoiseType)
				.Select(denoiseType =>
				{
					switch (denoiseType)
					{
						case VCDenoise.Off:
							return new List<ComboChoice>();
						case VCDenoise.hqdn3d:
							return this.GetFilterPresetChoices(hb_filter_ids.HB_FILTER_HQDN3D, "DenoisePreset_");
						case VCDenoise.NLMeans:
							return this.GetFilterPresetChoices(hb_filter_ids.HB_FILTER_NLMEANS, "DenoisePreset_");
						default:
							throw new ArgumentOutOfRangeException(nameof(denoiseType), denoiseType, null);
					}
				}).ToProperty(this, x => x.DenoisePresetChoices, out this.denoisePresetChoices);

			// DenoiseTuneVisible
			this.WhenAnyValue(x => x.DenoiseType, x => x.DenoisePreset, (denoiseType, denoisePreset) =>
			{
				return denoiseType == VCDenoise.NLMeans && denoisePreset != "custom";
			}).ToProperty(this, x => x.DenoiseTuneVisible, out this.denoiseTuneVisible);

			// CustomDenoiseVisible
			this.WhenAnyValue(x => x.DenoiseType, x => x.DenoisePreset, (denoiseType, denoisePreset) =>
			{
				return denoiseType != VCDenoise.Off && denoisePreset == "custom";
			}).ToProperty(this, x => x.CustomDenoiseVisible, out this.customDenoiseVisible);

			// DeblockText
			this.WhenAnyValue(x => x.Deblock, deblock =>
			{
				if (deblock >= MinDeblock)
				{
					return deblock.ToString(CultureInfo.CurrentCulture);
				}

				return CommonRes.Off;
			}).ToProperty(this, x => x.DeblockText, out this.deblockText);

			// The deinterlace and denoise presets need another nudge to change after the lists have changed.
			this.WhenAnyValue(x => x.DeinterlaceType)
				.Subscribe(_ =>
				{
					DispatchUtilities.BeginInvoke(() =>
					{
						this.RaisePropertyChanged(nameof(this.DeinterlacePreset));
					});
				});

			this.WhenAnyValue(x => x.DenoiseType)
				.Subscribe(_ =>
				{
					DispatchUtilities.BeginInvoke(() =>
					{
						this.RaisePropertyChanged(nameof(this.DenoisePreset));
					});
				});

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(nameof(this.Detelecine));
			this.RegisterProfileProperty(nameof(this.CustomDetelecine));

			this.RegisterProfileProperty(nameof(this.DeinterlaceType), () =>
			{
				if (this.DeinterlaceType != VCDeinterlace.Off)
				{
					if (string.IsNullOrEmpty(this.DeinterlacePreset))
					{
						this.DeinterlacePreset = "default";
					}

					if (string.IsNullOrEmpty(this.CombDetect))
					{
						this.CombDetect = "off";
					}
				}
			});
			this.RegisterProfileProperty(nameof(this.DeinterlacePreset));
			this.RegisterProfileProperty(nameof(this.CustomDeinterlace));
			this.RegisterProfileProperty(nameof(this.CombDetect));
			this.RegisterProfileProperty(nameof(this.CustomCombDetect));

			this.RegisterProfileProperty(nameof(this.DenoiseType), () =>
			{
				if (this.DenoiseType != VCDenoise.Off && string.IsNullOrEmpty(this.DenoisePreset))
				{
					this.DenoisePreset = "medium";
				}

				if (this.DenoiseType == VCDenoise.NLMeans && string.IsNullOrEmpty(this.DenoiseTune))
				{
					this.DenoiseTune = "none";
				}
			});

			this.RegisterProfileProperty(nameof(this.DenoisePreset));
			this.RegisterProfileProperty(nameof(this.DenoiseTune));
			this.RegisterProfileProperty(nameof(this.CustomDenoise));
			this.RegisterProfileProperty(nameof(this.Deblock));
			this.RegisterProfileProperty(nameof(this.Grayscale));
		}

		public List<ComboChoice> DetelecineChoices { get; }

		public string Detelecine
		{
			get { return this.Profile.Detelecine; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Detelecine), value); }
		}

		public string CustomDetelecine
		{
			get { return this.Profile.CustomDetelecine; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDetelecine), value); }
		}

		private ObservableAsPropertyHelper<bool> customDetelecineVisible;
		public bool CustomDetelecineVisible => this.customDetelecineVisible.Value;

		public List<ComboChoice<VCDeinterlace>> DeinterlaceChoices { get; }

		public VCDeinterlace DeinterlaceType
		{
			get { return this.Profile.DeinterlaceType; }
			set { this.UpdateProfileProperty(nameof(this.Profile.DeinterlaceType), value); }
		}

		private ObservableAsPropertyHelper<List<ComboChoice>> deinterlacePresetChoices;
		public List<ComboChoice> DeinterlacePresetChoices => this.deinterlacePresetChoices.Value;

		public string DeinterlacePreset
		{
			get { return this.Profile.DeinterlacePreset; }
			set
			{
				if (value == null)
				{
					return;
				}

				this.UpdateProfileProperty(nameof(this.Profile.DeinterlacePreset), value);
			}
		}

		public string CustomDeinterlace
		{
			get { return this.Profile.CustomDeinterlace; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDeinterlace), value); }
		}

		private ObservableAsPropertyHelper<bool> deinterlacePresetVisible;
		public bool DeinterlacePresetVisible => this.deinterlacePresetVisible.Value;

		private ObservableAsPropertyHelper<bool> customDeinterlaceVisible;
		public bool CustomDeinterlaceVisible => this.customDeinterlaceVisible.Value;

		public List<ComboChoice> CombDetectChoices { get; }

		public string CombDetect
		{
			get { return this.Profile.CombDetect; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CombDetect), value); }
		}

		public string CustomCombDetect
		{
			get { return this.Profile.CustomCombDetect; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomCombDetect), value); }
		}

		private ObservableAsPropertyHelper<bool> customCombDetectVisible;
		public bool CustomCombDetectVisible => this.customCombDetectVisible.Value;

		public List<ComboChoice<VCDenoise>> DenoiseChoices { get; }

		public VCDenoise DenoiseType
		{
			get { return this.Profile.DenoiseType; }
			set { this.UpdateProfileProperty(nameof(this.Profile.DenoiseType), value); }
		}

		private ObservableAsPropertyHelper<List<ComboChoice>> denoisePresetChoices;
		public List<ComboChoice> DenoisePresetChoices => this.denoisePresetChoices.Value;

		public string DenoisePreset
		{
			get
			{
				if (this.Profile.UseCustomDenoise)
				{
					return CustomDenoisePreset;
				}

				return this.Profile.DenoisePreset;
			}

			set
			{
				if (value == null)
				{
					return;
				}

				this.UpdateProfileProperty(nameof(this.Profile.DenoisePreset), value);
			}
		}

		public bool UseCustomDenoise
		{
			get { return this.Profile.UseCustomDenoise; }
			set { this.UpdateProfileProperty(nameof(this.Profile.UseCustomDenoise), value); }
		}

		private ObservableAsPropertyHelper<bool> denoisePresetVisible;
		public bool DenoisePresetVisible => this.denoisePresetVisible.Value;

		public List<ComboChoice> DenoiseTuneChoices { get; }

		public string DenoiseTune
		{
			get { return this.Profile.DenoiseTune; }
			set { this.UpdateProfileProperty(nameof(this.Profile.DenoiseTune), value); }
		}

		private ObservableAsPropertyHelper<bool> denoiseTuneVisible;
		public bool DenoiseTuneVisible => this.denoiseTuneVisible.Value;

		public string CustomDenoise
		{
			get { return this.Profile.CustomDenoise; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDenoise), value); }
		}

		private ObservableAsPropertyHelper<bool> customDenoiseVisible;
		public bool CustomDenoiseVisible => this.customDenoiseVisible.Value;

		public int Deblock
		{
			get { return this.Profile.Deblock; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Deblock), value); }
		}

		private ObservableAsPropertyHelper<string> deblockText;
		public string DeblockText => this.deblockText.Value;

		public bool Grayscale
		{
			get { return this.Profile.Grayscale; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Grayscale), value); }
		}

		private List<ComboChoice> GetFilterPresetChoices(hb_filter_ids filter, string resourcePrefix = null)
		{
			return ConvertParameterListToComboChoices(HandBrakeFilterHelpers.GetFilterPresets((int) filter), resourcePrefix);
		}

		private List<ComboChoice> GetFilterTuneChoices(hb_filter_ids filter, string resourcePrefix = null)
		{
			return ConvertParameterListToComboChoices(HandBrakeFilterHelpers.GetFilterTunes((int) filter), resourcePrefix);
		}

		private static List<ComboChoice> ConvertParameterListToComboChoices(IList<HBPresetTune> parameters, string resourcePrefix)
		{
			return parameters.Select(p =>
			{
				string friendlyName = p.Name;
				if (p.ShortName == "custom")
				{
					friendlyName = CommonRes.Custom;
				}
				else if (p.ShortName == "default")
				{
					friendlyName = CommonRes.Default;
				}
				else if (p.ShortName == "off")
				{
					friendlyName = CommonRes.Off;
				}
				else if (resourcePrefix != null)
				{
					string resourceString = EnumResourceManager.GetString(resourcePrefix + p.ShortName);
					if (!string.IsNullOrEmpty(resourceString))
					{
						friendlyName = resourceString;
					}
				}

				return new ComboChoice(p.ShortName, friendlyName);
			}).ToList();
		}
	}
}
