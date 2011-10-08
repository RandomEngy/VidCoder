using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	public class AdvancedPanelViewModel : PanelViewModel
	{
		private AdvancedChoice referenceFrames;
		private AdvancedChoice bFrames;
		private AdvancedChoice adaptiveBFrames;
		private AdvancedChoice directPrediction;
		private bool weightedPFrames;
		private AdvancedChoice pyramidalBFrames;
		private AdvancedChoice motionEstimationMethod;
		private AdvancedChoice subpixelMotionEstimation;
		private AdvancedChoice motionEstimationRange;
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

		public AdvancedPanelViewModel(EncodingViewModel encodingViewModel) : base(encodingViewModel)
		{
		}

		public AdvancedChoice ReferenceFrames
		{
			get
			{
				return this.referenceFrames;
			}

			set
			{
				this.referenceFrames = value;
				this.RaisePropertyChanged("ReferenceFrames");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("BFrames");
				this.RaisePropertyChanged("BFramesOptionsVisible");
				this.RaisePropertyChanged("PyramidalBFramesVisible");
				this.UpdateOptionsString();
			}
		}

		public bool BFramesOptionsVisible
		{
			get
			{
				return this.BFrames.Value != "0";
			}
		}

		public AdvancedChoice AdaptiveBFrames
		{
			get
			{
				return this.adaptiveBFrames;
			}

			set
			{
				this.adaptiveBFrames = value;
				this.RaisePropertyChanged("AdaptiveBFrames");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("DirectPrediction");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("WeightedPFrames");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("PyramidalBFrames");
				this.UpdateOptionsString();
			}
		}

		public bool PyramidalBFramesVisible
		{
			get
			{
				return int.Parse(this.BFrames.Value) > 1;
			}
		}

		public AdvancedChoice MotionEstimationMethod
		{
			get
			{
				return this.motionEstimationMethod;
			}

			set
			{
				this.motionEstimationMethod = value;
				this.RaisePropertyChanged("MotionEstimationMethod");
				this.RaisePropertyChanged("MotionEstimationRangeVisible");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("SubpixelMotionEstimation");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice MotionEstimationRange
		{
			get
			{
				return this.motionEstimationRange;
			}

			set
			{
				this.motionEstimationRange = value;
				this.RaisePropertyChanged("MotionEstimationRange");
				this.UpdateOptionsString();
			}
		}

		public bool MotionEstimationRangeVisible
		{
			get
			{
				string motionMethod = this.MotionEstimationMethod.Value;
				return motionMethod == "umh" || motionMethod == "esa" || motionMethod == "tesa";
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
				this.RaisePropertyChanged("Analysis");
				this.RaisePropertyChanged("EightByEightDctVisible");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("EightByEightDct");
				this.UpdateOptionsString();
			}
		}

		public bool EightByEightDctVisible
		{
			get
			{
				return this.Analysis.Value != "none";
			}
		}

		public bool CabacEntropyCoding
		{
			get
			{
				return this.cabacEntropyCoding;
			}

			set
			{
				this.cabacEntropyCoding = value;
				this.RaisePropertyChanged("CabacEntropyCoding");
				this.RaisePropertyChanged("PsychovisualTrellisVisible");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("Trellis");
				this.RaisePropertyChanged("PsychovisualTrellisVisible");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("AdaptiveQuantizationStrength");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("PsychovisualRateDistortion");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("PsychovisualTrellis");
				this.UpdateOptionsString();
			}
		}

		public bool PsychovisualTrellisVisible
		{
			get
			{
				return this.CabacEntropyCoding && this.Trellis.Value != "0";
			}
		}

		public AdvancedChoice DeblockingStrength
		{
			get
			{
				return this.deblockingStrength;
			}

			set
			{
				this.deblockingStrength = value;
				this.RaisePropertyChanged("DeblockingStrength");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("DeblockingThreshold");
				this.UpdateOptionsString();
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
				this.RaisePropertyChanged("NoDctDecimate");
				this.UpdateOptionsString();
			}
		}

		public string AdvancedOptionsString
		{
			get
			{
				return this.Profile.X264Options;
			}

			set
			{
				this.Profile.X264Options = value;
				this.UpdateUIFromAdvancedOptions();
				this.RaisePropertyChanged("AdvancedOptionsString");
				this.IsModified = true;
			}
		}

		public void UpdateUIFromAdvancedOptions()
		{
			this.AutomaticChange = true;

			// Reset UI to defaults, and re-apply options.
			this.SetAdvancedToDefaults();

			if (this.Profile.X264Options == null)
			{
				this.AutomaticChange = false;
				return;
			}

			// Check the updated options string. Update UI for any recognized options.
			string[] newOptionsSegments = this.Profile.X264Options.Split(':');
			foreach (string newOptionsSegment in newOptionsSegments)
			{
				int equalsIndex = newOptionsSegment.IndexOf('=');
				if (equalsIndex >= 0)
				{
					string optionName = newOptionsSegment.Substring(0, equalsIndex);
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
									newChoice = AdvancedChoices.ReferenceFrames.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.ReferenceFrames = newChoice;
									}
								}
								break;
							case "bframes":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.BFrames.SingleOrDefault(choice => choice.Value == parseInt.ToString());
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
							case "subq":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.SubpixelMotionEstimation.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.SubpixelMotionEstimation = newChoice;
									}
								}
								break;
							case "merange":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.MotionEstimationRange.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.MotionEstimationRange = newChoice;
									}
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
									newChoice = AdvancedChoices.Trellis.SingleOrDefault(choice => choice.Value == parseInt.ToString());
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
			this.MotionEstimationRange = AdvancedChoices.MotionEstimationRange.SingleOrDefault(choice => choice.IsDefault);
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
			if (this.AdvancedOptionsString != null)
			{
				string[] existingSegments = this.AdvancedOptionsString.Split(':');
				foreach (string existingSegment in existingSegments)
				{
					string optionName = existingSegment;
					int equalsIndex = existingSegment.IndexOf('=');
					if (equalsIndex >= 0)
					{
						optionName = existingSegment.Substring(0, existingSegment.IndexOf("="));
					}

					if (!this.uiOptions.Contains(optionName) && optionName != string.Empty)
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

			string motionEstimation = this.MotionEstimationMethod.Value;
			if ((motionEstimation == "umh" || motionEstimation == "esa" || motionEstimation == "tesa") && !this.MotionEstimationRange.IsDefault)
			{
				newOptions.Add("merange=" + this.MotionEstimationRange.Value);
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
				newOptions.Add("aq-strength=" + this.AdaptiveQuantizationStrength.ToString("F1"));
			}

			if (this.PsychovisualRateDistortion != 1.0 || psTrellis > 0.0)
			{
				newOptions.Add("psy-rd=" + this.PsychovisualRateDistortion.ToString("F1") + "," + psTrellis.ToString("F2"));
			}

			if (this.NoDctDecimate)
			{
				newOptions.Add("no-dct-decimate=1");
			}

			if (!this.DeblockingStrength.IsDefault || !this.DeblockingThreshold.IsDefault)
			{
				newOptions.Add("deblock=" + this.DeblockingStrength.Value + "," + this.DeblockingThreshold.Value);
			}

			this.Profile.X264Options = string.Join(":", newOptions);
			this.RaisePropertyChanged("AdvancedOptionsString");
			this.IsModified = true;
		}

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged("AdvancedOptionsString");
		}
	}
}
