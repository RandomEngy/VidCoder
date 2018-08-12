using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HandBrake.Interop.Interop;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class AdvancedPanelViewModel : PanelViewModel
	{
		private PresetsService presetsService = Resolver.Resolve<PresetsService>();

		private AdvancedChoice referenceFrames;
		private AdvancedChoice bFrames;
		private AdvancedChoice adaptiveBFrames;
		private AdvancedChoice directPrediction;
		private bool weightedPFrames;
		private AdvancedChoice pyramidalBFrames;
		private AdvancedChoice motionEstimationMethod;
		private AdvancedChoice subpixelMotionEstimation;
		private AdvancedChoice analysis;
		private bool eightByEightDct;
		private bool cabacEntropyCoding;
		private AdvancedChoice trellis;
		private double adaptiveQuantizationStrength;
		private double psychovisualRateDistortion;
		private double psychovisualTrellis;
		private AdvancedChoice deblockingStrength;
		private AdvancedChoice deblockingThreshold;
		private bool noDctDecimate;

		/// <summary>
		/// X264 options that have UI elements that correspond to them.
		/// </summary>
		private HashSet<string> uiOptions = new HashSet<string>
		{
			"ref", "bframes", "b-adapt", "direct", "weightp", "b-pyramid", "me", "subme", "subq", "merange",
			"analyse", "8x8dct", "cabac", "trellis", "aq-strength", "psy-rd", "no-dct-decimate", "deblock"
		};

		public AdvancedPanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
			: base(encodingWindowViewModel)
		{
			// X264CodecSelected
			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.VideoEncoder, videoEncoder =>
			{
				return videoEncoder == "x264";
			}).ToProperty(this, x => x.X264CodecSelected, out this.x264CodecSelected);

			// BFramesOptionsVisible
			this.WhenAnyValue(x => x.BFrames, bFrames =>
			{
				if (bFrames == null)
				{
					return false;
				}

				return bFrames.Value != "0";
			}).ToProperty(this, x => x.BFramesOptionsVisible, out this.bFramesOptionsVisible);

			// PyramidalBFramesVisible
			this.WhenAnyValue(x => x.BFrames, bFrames =>
			{
				if (bFrames == null)
				{
					return false;
				}

				return int.Parse(bFrames.Value, CultureInfo.InvariantCulture) > 1;
			}).ToProperty(this, x => x.PyramidalBFramesVisible, out this.pyramidalBFramesVisible);

			// EightByEightDctVisible
			this.WhenAnyValue(x => x.Analysis, analysis =>
			{
				if (analysis == null)
				{
					return false;
				}

				return analysis.Value != "none";
			}).ToProperty(this, x => x.EightByEightDctVisible, out this.eightByEightDctVisible);

			// PsychovisualTrellisVisible
			this.WhenAnyValue(x => x.CabacEntropyCoding, x => x.Trellis, (cabacEntropyCoding, trellis) =>
			{
				return cabacEntropyCoding && trellis != null && trellis.Value != "0";
			}).ToProperty(this, x => x.PsychovisualTrellisVisible, out this.psychovisualTrellisVisible);

			this.PresetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.VideoOptions)
				.Subscribe(_ =>
				{
					if (!this.updatingLocalVideoOptions)
					{
						this.RaisePropertyChanged(nameof(this.VideoOptions));
					}

					this.UpdateUIFromAdvancedOptions();
				});

			this.RegisterProfileProperty(nameof(this.Profile.VideoOptions));
		}

		private ObservableAsPropertyHelper<bool> x264CodecSelected;
		public bool X264CodecSelected => this.x264CodecSelected.Value;

		public AdvancedChoice ReferenceFrames
		{
			get
			{
				return this.referenceFrames;
			}

			set
			{
				this.referenceFrames = value;
				this.UpdateAdvancedProperty();
			}
		}

		public AdvancedChoice BFrames
		{
			get
			{
				return this.bFrames;
			}

			set
			{
				this.bFrames = value;
				this.UpdateAdvancedProperty();
			}
		}

		private ObservableAsPropertyHelper<bool> bFramesOptionsVisible;
		public bool BFramesOptionsVisible => this.bFramesOptionsVisible.Value;

		public AdvancedChoice AdaptiveBFrames
		{
			get
			{
				return this.adaptiveBFrames;
			}

			set
			{
				this.adaptiveBFrames = value;
				this.UpdateAdvancedProperty();
			}
		}

		public AdvancedChoice DirectPrediction
		{
			get
			{
				return this.directPrediction;
			}

			set
			{
				this.directPrediction = value;
				this.UpdateAdvancedProperty();
			}
		}

		public bool WeightedPFrames
		{
			get
			{
				return this.weightedPFrames;
			}

			set
			{
				this.weightedPFrames = value;
				this.UpdateAdvancedProperty();
			}
		}

		public AdvancedChoice PyramidalBFrames
		{
			get
			{
				return this.pyramidalBFrames;
			}

			set
			{
				this.pyramidalBFrames = value;
				this.UpdateAdvancedProperty();
			}
		}

		private ObservableAsPropertyHelper<bool> pyramidalBFramesVisible;
		public bool PyramidalBFramesVisible => this.pyramidalBFramesVisible.Value;

		public AdvancedChoice MotionEstimationMethod
		{
			get
			{
				return this.motionEstimationMethod;
			}

			set
			{
				this.motionEstimationMethod = value;
				this.CheckMotionEstimationRange();
				this.RaisePropertyChanged(nameof(this.MotionEstimationRange));

				this.UpdateAdvancedProperty();
			}
		}

		public AdvancedChoice SubpixelMotionEstimation
		{
			get
			{
				return this.subpixelMotionEstimation;
			}

			set
			{
				this.subpixelMotionEstimation = value;
				this.UpdateAdvancedProperty();
			}
		}

		private int motionEstimationRange;
		public int MotionEstimationRange
		{
			get
			{
				return this.motionEstimationRange;
			}

			set
			{
				this.motionEstimationRange = value;
				this.CheckMotionEstimationRange();

				this.UpdateAdvancedProperty();
			}
		}

		private void CheckMotionEstimationRange()
		{
			if ((MotionEstimationMethod.Value == "hex" || MotionEstimationMethod.Value == "dia") && (this.motionEstimationRange > 16))
			{
				this.motionEstimationRange = 16;
			}
			else if (this.motionEstimationRange < 4)
			{
				this.motionEstimationRange = 4;
			}
		}

		public AdvancedChoice Analysis
		{
			get
			{
				return this.analysis;
			}

			set
			{
				this.analysis = value;
				this.UpdateAdvancedProperty();
			}
		}

		public bool EightByEightDct
		{
			get
			{
				return this.eightByEightDct;
			}

			set
			{
				this.eightByEightDct = value;
				this.UpdateAdvancedProperty();
			}
		}

		private ObservableAsPropertyHelper<bool> eightByEightDctVisible;
		public bool EightByEightDctVisible => this.eightByEightDctVisible.Value;

		public bool CabacEntropyCoding
		{
			get
			{
				return this.cabacEntropyCoding;
			}

			set
			{
				this.cabacEntropyCoding = value;
				this.UpdateAdvancedProperty();
			}
		}

		public AdvancedChoice Trellis
		{
			get
			{
				return this.trellis;
			}

			set
			{
				this.trellis = value;
				this.UpdateAdvancedProperty();
			}
		}

		public double AdaptiveQuantizationStrength
		{
			get
			{
				return this.adaptiveQuantizationStrength;
			}
			set
			{
				this.adaptiveQuantizationStrength = value;
				this.UpdateAdvancedProperty();
			}
		}

		public double PsychovisualRateDistortion
		{
			get
			{
				return this.psychovisualRateDistortion;
			}

			set
			{
				this.psychovisualRateDistortion = value;
				this.UpdateAdvancedProperty();
			}
		}

		public double PsychovisualTrellis
		{
			get
			{
				return this.psychovisualTrellis;
			}

			set
			{
				this.psychovisualTrellis = value;
				this.UpdateAdvancedProperty();
			}
		}

		private ObservableAsPropertyHelper<bool> psychovisualTrellisVisible;
		public bool PsychovisualTrellisVisible => this.psychovisualTrellisVisible.Value;

		public AdvancedChoice DeblockingStrength
		{
			get
			{
				return this.deblockingStrength;
			}

			set
			{
				this.deblockingStrength = value;
				this.UpdateAdvancedProperty();
			}
		}

		public AdvancedChoice DeblockingThreshold
		{
			get
			{
				return this.deblockingThreshold;
			}

			set
			{
				this.deblockingThreshold = value;
				this.UpdateAdvancedProperty();
			}
		}

		public bool NoDctDecimate
		{
			get
			{
				return this.noDctDecimate;
			}

			set
			{
				this.noDctDecimate = value;
				this.UpdateAdvancedProperty();
			}
		}

		private bool updatingLocalVideoOptions;

		public string VideoOptions
		{
			get { return this.Profile.VideoOptions; }
			set
			{
				this.updatingLocalVideoOptions = true;
				this.UpdateProfileProperty(nameof(this.Profile.VideoOptions), value);
				this.updatingLocalVideoOptions = false;
			}
		}

		private void UpdateUIFromAdvancedOptions()
		{
			this.AutomaticChange = true;

			// Reset UI to defaults, and re-apply options.
			this.SetAdvancedToDefaults();

			if (this.Profile.VideoOptions == null)
			{
				this.AutomaticChange = false;
				return;
			}

			// Check the updated options string. Update UI for any recognized options.
			string[] newOptionsSegments = this.Profile.VideoOptions.Split(':');
			foreach (string newOptionsSegment in newOptionsSegments)
			{
				int equalsIndex = newOptionsSegment.IndexOf('=');
				if (equalsIndex >= 0)
				{
					string optionName = HandBrakeUtils.SanitizeX264OptName(newOptionsSegment.Substring(0, equalsIndex));
					string optionValue = newOptionsSegment.Substring(equalsIndex + 1);

					if (optionName != string.Empty && optionValue != string.Empty)
					{
						AdvancedChoice newChoice;
						int parseInt;
						double parseDouble;
						string[] subParts;

						switch (optionName)
						{
							case "ref":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.ReferenceFrames.SingleOrDefault(choice => choice.Value == parseInt.ToString(CultureInfo.InvariantCulture));
									if (newChoice != null)
									{
										this.ReferenceFrames = newChoice;
									}
								}
								break;
							case "bframes":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.BFrames.SingleOrDefault(choice => choice.Value == parseInt.ToString(CultureInfo.InvariantCulture));
									if (newChoice != null)
									{
										this.BFrames = newChoice;
									}
								}
								break;
							case "b-adapt":
								newChoice = AdvancedChoices.AdaptiveBFrames.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.AdaptiveBFrames = newChoice;
								}
								break;
							case "direct":
								newChoice = AdvancedChoices.DirectPrediction.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.DirectPrediction = newChoice;
								}
								break;
							case "weightp":
								if (optionValue == "0")
								{
									this.WeightedPFrames = false;
								}
								else if (optionValue == "1")
								{
									this.WeightedPFrames = true;
								}
								break;
							case "b-pyramid":
								newChoice = AdvancedChoices.PyramidalBFrames.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.PyramidalBFrames = newChoice;
								}
								break;
							case "me":
								newChoice = AdvancedChoices.MotionEstimationMethod.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.MotionEstimationMethod = newChoice;
								}
								break;
							case "subme":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.SubpixelMotionEstimation.SingleOrDefault(choice => choice.Value == parseInt.ToString(CultureInfo.InvariantCulture));
									if (newChoice != null)
									{
										this.SubpixelMotionEstimation = newChoice;
									}
								}
								break;
							case "merange":
								if (int.TryParse(optionValue, out parseInt))
								{
									this.MotionEstimationRange = parseInt;
								}
								break;
							case "analyse":
								newChoice = AdvancedChoices.Analysis.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.Analysis = newChoice;
								}
								break;
							case "8x8dct":
								if (optionValue == "0")
								{
									this.EightByEightDct = false;
								}
								else if (optionValue == "1")
								{
									this.EightByEightDct = true;
								}
								break;
							case "cabac":
								if (optionValue == "0")
								{
									this.CabacEntropyCoding = false;
								}
								else if (optionValue == "1")
								{
									this.CabacEntropyCoding = true;
								}
								break;
							case "trellis":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.Trellis.SingleOrDefault(choice => choice.Value == parseInt.ToString(CultureInfo.InvariantCulture));
									if (newChoice != null)
									{
										this.Trellis = newChoice;
									}
								}
								break;
							case "aq-strength":
								if (double.TryParse(optionValue, out parseDouble) && parseDouble >= 0.0 && parseDouble <= 2.0)
								{
									this.AdaptiveQuantizationStrength = Math.Round(parseDouble, 1);
								}
								break;
							case "psy-rd":
								subParts = optionValue.Split(',');
								if (subParts.Length == 2)
								{
									double psyRD, psyTrellis;
									if (double.TryParse(subParts[0], out psyRD) && double.TryParse(subParts[1], out psyTrellis))
									{
										if (psyRD >= 0.0 && psyRD <= 2.0 && psyTrellis >= 0.0 && psyTrellis <= 1.0)
										{
											this.PsychovisualRateDistortion = Math.Round(psyRD, 1);
											this.PsychovisualTrellis = Math.Round(psyTrellis, 2);
										}
									}
								}
								break;
							case "no-dct-decimate":
								if (optionValue == "0")
								{
									this.NoDctDecimate = false;
								}
								else if (optionValue == "1")
								{
									this.NoDctDecimate = true;
								}
								break;
							case "deblock":
								subParts = optionValue.Split(',');
								if (subParts.Length == 2)
								{
									int dbStrength, dbThreshold;
									if (int.TryParse(subParts[0], out dbStrength) && int.TryParse(subParts[1], out dbThreshold))
									{
										newChoice = AdvancedChoices.DeblockingStrength.SingleOrDefault(choice => choice.Value == subParts[0]);
										if (newChoice != null)
										{
											this.DeblockingStrength = newChoice;
										}

										newChoice = AdvancedChoices.DeblockingThreshold.SingleOrDefault(choice => choice.Value == subParts[1]);
										if (newChoice != null)
										{
											this.DeblockingThreshold = newChoice;
										}
									}
								}
								break;
							default:
								break;
						}
					}
				}
			}

			this.AutomaticChange = false;
		}

		private void SetAdvancedToDefaults()
		{
			this.ReferenceFrames = AdvancedChoices.ReferenceFrames.SingleOrDefault(choice => choice.IsDefault);
			this.BFrames = AdvancedChoices.BFrames.SingleOrDefault(choice => choice.IsDefault);
			this.AdaptiveBFrames = AdvancedChoices.AdaptiveBFrames.SingleOrDefault(choice => choice.IsDefault);
			this.DirectPrediction = AdvancedChoices.DirectPrediction.SingleOrDefault(choice => choice.IsDefault);
			this.WeightedPFrames = true;
			this.PyramidalBFrames = AdvancedChoices.PyramidalBFrames.SingleOrDefault(choice => choice.IsDefault);
			this.MotionEstimationMethod = AdvancedChoices.MotionEstimationMethod.SingleOrDefault(choice => choice.IsDefault);
			this.SubpixelMotionEstimation = AdvancedChoices.SubpixelMotionEstimation.SingleOrDefault(choice => choice.IsDefault);
			this.MotionEstimationRange = 16;
			this.Analysis = AdvancedChoices.Analysis.SingleOrDefault(choice => choice.IsDefault);
			this.EightByEightDct = true;
			this.CabacEntropyCoding = true;
			this.Trellis = AdvancedChoices.Trellis.SingleOrDefault(choice => choice.IsDefault);
			this.AdaptiveQuantizationStrength = 1.0;
			this.PsychovisualRateDistortion = 1.0;
			this.PsychovisualTrellis = 0.0;
			this.DeblockingStrength = AdvancedChoices.DeblockingStrength.SingleOrDefault(choice => choice.IsDefault);
			this.DeblockingThreshold = AdvancedChoices.DeblockingThreshold.SingleOrDefault(choice => choice.IsDefault);
			this.NoDctDecimate = false;
		}

		private void UpdateAdvancedProperty([CallerMemberName] string callerName = null)
		{
			this.RaisePropertyChanged(callerName);
			this.UpdateOptionsString();
		}

		/// <summary>
		/// Update the x264 options string from a UI change.
		/// </summary>
		private void UpdateOptionsString()
		{
			if (this.AutomaticChange)
			{
				return;
			}

			List<string> newOptions = new List<string>();

			// First add any parts of the options string that don't correspond to the UI
			if (this.VideoOptions != null)
			{
				string[] existingSegments = this.VideoOptions.Split(':');
				foreach (string existingSegment in existingSegments)
				{
					string optionName = existingSegment;
					int equalsIndex = existingSegment.IndexOf('=');
					if (equalsIndex >= 0)
					{
						optionName = existingSegment.Substring(0, existingSegment.IndexOf("=", StringComparison.Ordinal));
					}

					if (optionName != string.Empty && !this.uiOptions.Contains(HandBrakeUtils.SanitizeX264OptName(optionName)))
					{
						newOptions.Add(existingSegment);
					}
				}
			}

			// Now add everything from the UI
			if (!this.ReferenceFrames.IsDefault)
			{
				newOptions.Add("ref=" + this.ReferenceFrames.Value);
			}

			if (!this.BFrames.IsDefault)
			{
				newOptions.Add("bframes=" + this.BFrames.Value);
			}

			if (this.BFrames.Value != "0")
			{
				if (!this.AdaptiveBFrames.IsDefault)
				{
					newOptions.Add("b-adapt=" + this.AdaptiveBFrames.Value);
				}

				if (!this.DirectPrediction.IsDefault)
				{
					newOptions.Add("direct=" + this.DirectPrediction.Value);
				}

				if (this.BFrames.Value != "1" && !this.PyramidalBFrames.IsDefault)
				{
					newOptions.Add("b-pyramid=" + this.PyramidalBFrames.Value);
				}
			}

			if (!this.WeightedPFrames)
			{
				newOptions.Add("weightp=0");
			}

			if (!this.MotionEstimationMethod.IsDefault)
			{
				newOptions.Add("me=" + this.MotionEstimationMethod.Value);
			}

			if (!this.SubpixelMotionEstimation.IsDefault)
			{
				newOptions.Add("subme=" + this.SubpixelMotionEstimation.Value);
			}

			if (this.MotionEstimationRange != 16)
			{
				newOptions.Add("merange=" + this.MotionEstimationRange);
			}

			if (!this.Analysis.IsDefault)
			{
				newOptions.Add("analyse=" + this.Analysis.Value);
			}

			if (this.Analysis.Value != "none" && !this.EightByEightDct)
			{
				newOptions.Add("8x8dct=0");
			}

			if (!this.CabacEntropyCoding)
			{
				newOptions.Add("cabac=0");
			}

			if (!this.Trellis.IsDefault)
			{
				newOptions.Add("trellis=" + this.Trellis.Value);
			}

			double psTrellis = 0.0;
			if (this.CabacEntropyCoding && this.Trellis.Value != "0")
			{
				psTrellis = this.PsychovisualTrellis;
			}

			if (this.AdaptiveQuantizationStrength != 1.0)
			{
				newOptions.Add("aq-strength=" + this.AdaptiveQuantizationStrength.ToString("F1", CultureInfo.InvariantCulture));
			}

			if (this.PsychovisualRateDistortion != 1.0 || psTrellis > 0.0)
			{
				newOptions.Add("psy-rd=" + this.PsychovisualRateDistortion.ToString("F1", CultureInfo.InvariantCulture) + "," + psTrellis.ToString("F2", CultureInfo.InvariantCulture));
			}

			if (this.NoDctDecimate)
			{
				newOptions.Add("no-dct-decimate=1");
			}

			if (!this.DeblockingStrength.IsDefault || !this.DeblockingThreshold.IsDefault)
			{
				newOptions.Add("deblock=" + this.DeblockingStrength.Value + "," + this.DeblockingThreshold.Value);
			}

			this.VideoOptions = string.Join(":", newOptions);
		}
	}
}
