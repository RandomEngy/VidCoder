using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.HbLib;
using HandBrake.Interop.Interop.Interfaces.Model.Filters;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities;

namespace VidCoder.ViewModel
{
	public class VideoFiltersPanelViewModel : PanelViewModel, INotifyPropertyChanged
	{
		private const string CustomDenoisePreset = "custom";
		private const int MinDeblock = 5;

		private static readonly ResourceManager EnumResourceManager = new ResourceManager(typeof(EnumsRes));

		private PreviewUpdateService previewUpdateService = StaticResolver.Resolve<PreviewUpdateService>();

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
				new ComboChoice<VCDenoise>(VCDenoise.ChromaSmooth, EnumsRes.Denoise_ChromaSmooth)
            };

			this.SharpenChoices = new List<ComboChoice<VCSharpen>>
			{
				new ComboChoice<VCSharpen>(VCSharpen.Off, CommonRes.Off),
				new ComboChoice<VCSharpen>(VCSharpen.UnSharp, EnumsRes.Sharpen_UnSharp),
				new ComboChoice<VCSharpen>(VCSharpen.LapSharp, EnumsRes.Sharpen_LapSharp),
			};

			// CustomDetelecineVisible
			this.WhenAnyValue(x => x.Detelecine, detelecine =>
			{
				return detelecine == "custom";
			}).ToProperty(this, x => x.CustomDetelecineVisible, out this.customDetelecineVisible);

			// DeinterlacePresetChoices
			this.WhenAnyValue(x => x.DeinterlaceType)
				.Select(deinterlaceType =>
				{
					if (deinterlaceType == VCDeinterlace.Off)
					{
						return new List<ComboChoice>();
					}

					return this.GetFilterPresetChoices(GetDeinterlaceFilter(deinterlaceType), "DeinterlacePreset_");
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

			// CustomDeinterlaceToolTip
			this.WhenAnyValue(x => x.DeinterlaceType, deinterlaceType =>
			{
				if (deinterlaceType == VCDeinterlace.Off)
				{
					return string.Empty;
				}

				return GetCustomFilterToolTip(GetDeinterlaceFilter(deinterlaceType));
			}).ToProperty(this, x => x.CustomDeinterlaceToolTip, out this.customDeinterlaceToolTip);

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
					if (denoiseType == VCDenoise.Off)
					{
						return new List<ComboChoice>();
					}

					return this.GetFilterPresetChoices(ModelConverters.VCDenoiseToHbDenoise(denoiseType), "DenoisePreset_");
				}).ToProperty(this, x => x.DenoisePresetChoices, out this.denoisePresetChoices);

			// DenoiseTuneVisible
			this.WhenAnyValue(x => x.DenoiseType, x => x.DenoisePreset, (denoiseType, denoisePreset) =>
			{
				return (denoiseType == VCDenoise.NLMeans || denoiseType == VCDenoise.ChromaSmooth) && denoisePreset != "custom";
			}).ToProperty(this, x => x.DenoiseTuneVisible, out this.denoiseTuneVisible);

			// DenoiseTuneChoices
			this.WhenAnyValue(x => x.DenoiseType)
				.Select(denoiseType =>
				{
					if (denoiseType == VCDenoise.Off || denoiseType == VCDenoise.hqdn3d)
					{
						return new List<ComboChoice>();
					}

					return this.GetFilterTuneChoices(ModelConverters.VCDenoiseToHbDenoise(denoiseType), "DenoiseTune_");
				}).ToProperty(this, x => x.DenoiseTuneChoices, out this.denoiseTuneChoices);

			// CustomDenoiseVisible
			this.WhenAnyValue(x => x.DenoiseType, x => x.DenoisePreset, (denoiseType, denoisePreset) =>
			{
				return denoiseType != VCDenoise.Off && denoisePreset == "custom";
			}).ToProperty(this, x => x.CustomDenoiseVisible, out this.customDenoiseVisible);

			// CustomDenoiseToolTip
			this.WhenAnyValue(x => x.DenoiseType, denoiseType =>
			{
				if (denoiseType == VCDenoise.Off)
				{
					return string.Empty;
				}

				return GetCustomFilterToolTip(ModelConverters.VCDenoiseToHbDenoise(denoiseType));
			}).ToProperty(this, x => x.CustomDenoiseToolTip, out this.customDenoiseToolTip);

			// SharpenPresetChoices
			this.WhenAnyValue(x => x.SharpenType)
				.Select(sharpenType =>
				{
					if (sharpenType == VCSharpen.Off)
					{
						return new List<ComboChoice>();
					}

					return this.GetFilterPresetChoices(GetSharpenFilter(sharpenType));
				}).ToProperty(this, x => x.SharpenPresetChoices, out this.sharpenPresetChoices);

			// SharpenPresetVisible
			this.WhenAnyValue(x => x.SharpenType)
				.Select(sharpenType =>
				{
					return sharpenType != VCSharpen.Off;
				}).ToProperty(this, x => x.SharpenPresetVisible, out this.sharpenPresetVisible);

			// SharpenTuneChoices
			this.WhenAnyValue(x => x.SharpenType)
				.Select(sharpenType =>
				{
					if (sharpenType == VCSharpen.Off)
					{
						return new List<ComboChoice>();
					}

					return this.GetFilterTuneChoices(GetSharpenFilter(sharpenType));
				}).ToProperty(this, x => x.SharpenTuneChoices, out this.sharpenTuneChoices);

			// SharpenTuneVisible
			this.WhenAnyValue(x => x.SharpenType, x => x.SharpenPreset, (sharpenType, sharpenPreset) =>
			{
				return sharpenType != VCSharpen.Off && sharpenPreset != "custom";
			}).ToProperty(this, x => x.SharpenTuneVisible, out this.sharpenTuneVisible);

			// CustomSharpenVisible
			this.WhenAnyValue(x => x.SharpenType, x => x.SharpenPreset, (sharpenType, sharpenPreset) =>
			{
				return sharpenType != VCSharpen.Off && sharpenPreset == "custom";
			}).ToProperty(this, x => x.CustomSharpenVisible, out this.customSharpenVisible);

			// CustomSharpenToolTip
			this.WhenAnyValue(x => x.SharpenType)
				.Select(sharpenType =>
				{
					if (sharpenType == VCSharpen.Off)
					{
						return string.Empty;
					}

					return GetCustomFilterToolTip(GetSharpenFilter(sharpenType));
				}).ToProperty(this, x => x.CustomSharpenToolTip, out this.customSharpenToolTip);

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
			//this.WhenAnyValue(x => x.DeinterlaceType)
			//	.Subscribe(_ =>
			//	{
			//		DispatchUtilities.BeginInvoke(() =>
			//		{
			//			this.RaisePropertyChanged(nameof(this.DeinterlacePreset));
			//			var oldValue = this.DeinterlacePreset;
			//			this.DeinterlacePreset = "";
			//			this.DeinterlacePreset = oldValue;
			//		});
			//	});

			//this.WhenAnyValue(x => x.DenoiseType)
			//	.Subscribe(_ =>
			//	{
			//		DispatchUtilities.BeginInvoke(() =>
			//		{
			//			this.RaisePropertyChanged(nameof(this.DenoisePreset));
			//			var oldValue = this.DenoisePreset;
			//			this.DenoisePreset = "";
			//			this.DenoisePreset = oldValue;
			//		});
			//	});

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(nameof(this.Detelecine), () =>
			{
				if (this.Detelecine == "custom" && string.IsNullOrWhiteSpace(this.CustomDetelecine))
				{
					this.CustomDetelecine = GetDefaultCustomFilterString(hb_filter_ids.HB_FILTER_DETELECINE);
				}
			});
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

				// Needs a kick to actually get it to change
				var oldPreset = this.DeinterlacePreset;
				this.DeinterlacePreset = string.Empty;
				this.DeinterlacePreset = oldPreset;

				this.previewUpdateService.RefreshPreview();
			});
			this.RegisterProfileProperty(nameof(this.DeinterlacePreset), () =>
			{
				if (this.DeinterlacePreset == "custom" && string.IsNullOrWhiteSpace(this.CustomDeinterlace))
				{
					if (this.DeinterlaceType == VCDeinterlace.Off)
					{
						return;
					}

					this.CustomDeinterlace = GetDefaultCustomFilterString(GetDeinterlaceFilter(this.DeinterlaceType));
				}
			});
			this.RegisterProfileProperty(nameof(this.CustomDeinterlace));
			this.RegisterProfileProperty(nameof(this.CombDetect), () =>
			{
				if (this.CombDetect == "custom" && string.IsNullOrWhiteSpace(this.CustomCombDetect))
				{
					this.CustomCombDetect = GetDefaultCustomFilterString(hb_filter_ids.HB_FILTER_COMB_DETECT);
				}
			});
			this.RegisterProfileProperty(nameof(this.CustomCombDetect));

			this.RegisterProfileProperty(nameof(this.DenoiseType), () =>
			{
				if (this.DenoiseType != VCDenoise.Off && string.IsNullOrEmpty(this.DenoisePreset))
				{
					this.DenoisePreset = "medium";
				}

				if ((this.DenoiseType == VCDenoise.NLMeans || this.DenoiseType == VCDenoise.ChromaSmooth) && string.IsNullOrEmpty(this.DenoiseTune))
				{
					this.DenoiseTune = "none";
				}

				if (this.DenoiseType != VCDenoise.Off && this.DenoisePreset == "custom")
				{
					this.CustomDenoise = GetDefaultCustomFilterString(ModelConverters.VCDenoiseToHbDenoise(this.DenoiseType));
				}

				// Needs a kick to actually get it to change
				var oldPreset = this.DenoisePreset;
				this.DenoisePreset = string.Empty;
				this.DenoisePreset = oldPreset;
			});

			this.RegisterProfileProperty(nameof(this.DenoisePreset), () =>
			{
				if (this.DenoisePreset == "custom")
				{
					if (this.DenoiseType == VCDenoise.Off)
					{
						return;
					}

					this.CustomDenoise = GetDefaultCustomFilterString(ModelConverters.VCDenoiseToHbDenoise(this.DenoiseType));
				}
			});
			this.RegisterProfileProperty(nameof(this.DenoiseTune));
			this.RegisterProfileProperty(nameof(this.CustomDenoise));
			this.RegisterProfileProperty(nameof(this.SharpenType), () =>
			{
				if (this.SharpenType != VCSharpen.Off && string.IsNullOrEmpty(this.SharpenPreset))
				{
					this.SharpenPreset = "medium";
				}

				this.RaisePropertyChanged(nameof(this.SharpenPreset));

				if (this.SharpenType != VCSharpen.Off && string.IsNullOrEmpty(this.SharpenTune))
				{
					this.SharpenTune = "none";
				}

				this.RaisePropertyChanged(nameof(this.SharpenTune));

				if (this.SharpenType != VCSharpen.Off && this.SharpenPreset == "custom")
				{
					this.CustomSharpen = GetDefaultCustomFilterString(GetSharpenFilter(this.SharpenType));
				}

				// Needs a kick to actually get it to change
				var oldPreset = this.SharpenPreset;
				this.SharpenPreset = string.Empty;
				this.SharpenPreset = oldPreset;
			});

			this.RegisterProfileProperty(nameof(this.SharpenPreset), () =>
			{
				if (this.SharpenPreset == "custom")
				{
					if (this.SharpenType == VCSharpen.Off)
					{
						return;
					}

					this.CustomSharpen = GetDefaultCustomFilterString(GetSharpenFilter(this.SharpenType));
				}
			});
			this.RegisterProfileProperty(nameof(this.SharpenTune));
			this.RegisterProfileProperty(nameof(this.CustomSharpen));
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

		public string CustomDetelecineToolTip => GetCustomFilterToolTip(hb_filter_ids.HB_FILTER_DETELECINE);

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
			get { return AddSpacesAfterColons(this.Profile.CustomDeinterlace); }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomDeinterlace), RemoveSpacesAfterColons(value)); }
		}

		private ObservableAsPropertyHelper<bool> deinterlacePresetVisible;
		public bool DeinterlacePresetVisible => this.deinterlacePresetVisible.Value;

		private ObservableAsPropertyHelper<bool> customDeinterlaceVisible;
		public bool CustomDeinterlaceVisible => this.customDeinterlaceVisible.Value;

		private ObservableAsPropertyHelper<string> customDeinterlaceToolTip;
		public string CustomDeinterlaceToolTip => this.customDeinterlaceToolTip.Value;

		public List<ComboChoice> CombDetectChoices { get; }

		public string CombDetect
		{
			get { return this.Profile.CombDetect; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CombDetect), value); }
		}

		public string CustomCombDetect
		{
			get { return AddSpacesAfterColons(this.Profile.CustomCombDetect); }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomCombDetect), RemoveSpacesAfterColons(value)); }
		}

		private ObservableAsPropertyHelper<bool> customCombDetectVisible;
		public bool CustomCombDetectVisible => this.customCombDetectVisible.Value;

		public string CustomCombDetectToolTip => GetCustomFilterToolTip(hb_filter_ids.HB_FILTER_COMB_DETECT);

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
			get { return this.Profile.DenoisePreset; }

			set
			{
				if (value == null)
				{
					return;
				}

				this.UpdateProfileProperty(nameof(this.Profile.DenoisePreset), value);
			}
		}

		private ObservableAsPropertyHelper<bool> denoisePresetVisible;
		public bool DenoisePresetVisible => this.denoisePresetVisible.Value;

		private ObservableAsPropertyHelper<List<ComboChoice>> denoiseTuneChoices;
		public List<ComboChoice> DenoiseTuneChoices => this.denoiseTuneChoices.Value;

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

		private ObservableAsPropertyHelper<string> customDenoiseToolTip;
		public string CustomDenoiseToolTip => this.customDenoiseToolTip.Value;

		public List<ComboChoice<VCSharpen>> SharpenChoices { get; }

		public VCSharpen SharpenType
		{
			get { return this.Profile.SharpenType; }
			set { this.UpdateProfileProperty(nameof(this.Profile.SharpenType), value); }
		}

		private ObservableAsPropertyHelper<List<ComboChoice>> sharpenPresetChoices;
		public List<ComboChoice> SharpenPresetChoices => this.sharpenPresetChoices.Value;

		public string SharpenPreset
		{
			get { return this.Profile.SharpenPreset; }

			set
			{
				if (value == null)
				{
					return;
				}

				this.UpdateProfileProperty(nameof(this.Profile.SharpenPreset), value);
			}
		}

		private ObservableAsPropertyHelper<bool> sharpenPresetVisible;
		public bool SharpenPresetVisible => this.sharpenPresetVisible.Value;

		private ObservableAsPropertyHelper<List<ComboChoice>> sharpenTuneChoices;
		public List<ComboChoice> SharpenTuneChoices => this.sharpenTuneChoices.Value;

		public string SharpenTune
		{
			get { return this.Profile.SharpenTune; }
			set { this.UpdateProfileProperty(nameof(this.Profile.SharpenTune), value); }
		}

		private ObservableAsPropertyHelper<bool> sharpenTuneVisible;
		public bool SharpenTuneVisible => this.sharpenTuneVisible.Value;

		public string CustomSharpen
		{
			get { return this.Profile.CustomSharpen; }
			set { this.UpdateProfileProperty(nameof(this.Profile.CustomSharpen), value); }
		}

		private ObservableAsPropertyHelper<bool> customSharpenVisible;
		public bool CustomSharpenVisible => this.customSharpenVisible.Value;

		private ObservableAsPropertyHelper<string> customSharpenToolTip;
		public string CustomSharpenToolTip => this.customSharpenToolTip.Value;

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
			return ConvertParameterListToComboChoices(HandBrakeFilterHelpers.GetFilterPresets((int)filter), resourcePrefix);
		}

		private List<ComboChoice> GetFilterTuneChoices(hb_filter_ids filter, string resourcePrefix = null)
		{
			return ConvertParameterListToComboChoices(HandBrakeFilterHelpers.GetFilterTunes((int)filter), resourcePrefix);
		}

		private static string AddSpacesAfterColons(string input)
		{
			if (input == null)
			{
				return null;
			}

			return input.Replace(":", ": ");
		}

		private static string RemoveSpacesAfterColons(string input)
		{
			if (input == null)
			{
				return null;
			}

			var result = new StringBuilder();
			bool lastCharWasColon = false;

			for (int i = 0; i < input.Length; i++)
			{
				char c = input[i];
				if (c == ':')
				{
					lastCharWasColon = true;
					result.Append(c);
				}
				else
				{
					if (c != ' ' || !lastCharWasColon || i == input.Length - 1)
					{
						result.Append(c);
					}

					lastCharWasColon = false;
				}
			}

			return result.ToString();
		}

		private static string GetDefaultCustomFilterString(hb_filter_ids filter)
		{
			IDictionary<string, string> customDictionary = HandBrakeFilterHelpers.GetDefaultCustomSettings((int)filter);
			List<string> keyValuePairList = new List<string>();
			foreach (KeyValuePair<string, string> keyValuePair in customDictionary)
			{
				keyValuePairList.Add(keyValuePair.Key + "=" + keyValuePair.Value);
			}

			return string.Join(":", keyValuePairList);
		}

		private static string GetCustomFilterToolTip(hb_filter_ids filter)
		{
			var builder = new StringBuilder(EncodingRes.CustomFilterToolTipFormatPart);
			builder.AppendLine();
			builder.Append(EncodingRes.CustomFilterToolTipKeysLabel);

			IList<string> keys = HandBrakeFilterHelpers.GetFilterKeys((int)filter);
			foreach (string key in keys)
			{
				builder.AppendLine();
				builder.Append("    ");
				builder.Append(key);
			}

			return builder.ToString();
		}

		private static hb_filter_ids GetDeinterlaceFilter(VCDeinterlace deinterlaceType)
		{
			switch (deinterlaceType)
			{
				case VCDeinterlace.Yadif:
					return hb_filter_ids.HB_FILTER_DEINTERLACE;
				case VCDeinterlace.Decomb:
					return hb_filter_ids.HB_FILTER_DECOMB;
				default:
					throw new ArgumentOutOfRangeException(nameof(deinterlaceType), $"Could not find filter for deinterlace type {deinterlaceType}.");
			}
		}

		private static hb_filter_ids GetSharpenFilter(VCSharpen sharpenType)
		{
			switch (sharpenType)
			{
				case VCSharpen.UnSharp:
					return hb_filter_ids.HB_FILTER_UNSHARP;
				case VCSharpen.LapSharp:
					return hb_filter_ids.HB_FILTER_LAPSHARP;
				default:
					throw new ArgumentOutOfRangeException(nameof(sharpenType), $"Could not find filter for sharpen type {sharpenType}.");
			}
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
