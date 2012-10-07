using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	using LocalResources;

	public class AdvancedChoice
	{
		/// <summary>
		/// Gets or sets a value indicating whether the given choice is default.
		/// </summary>
		public bool IsDefault { get; set; }

		/// <summary>
		/// Gets or sets the UI label for the choice.
		/// </summary>
		public string Label { get; set; }

		/// <summary>
		/// Gets or sets the value on the options string for the choice.
		/// </summary>
		public string Value { get; set; }
	}

	public static class AdvancedChoices
	{
		private static List<AdvancedChoice> referenceFrames;
		private static List<AdvancedChoice> bFrames;
		private static List<AdvancedChoice> pyramidalBFrames;
		private static List<AdvancedChoice> adaptiveBFrames;
		private static List<AdvancedChoice> directPrediction;
		private static List<AdvancedChoice> motionEstimationMethod;
		private static List<AdvancedChoice> subpixelMotionEstimation;
		private static List<AdvancedChoice> motionEstimationRange;
		private static List<AdvancedChoice> analysis;
		private static List<AdvancedChoice> trellis;
		private static List<AdvancedChoice> deblockingStrength;
		private static List<AdvancedChoice> deblockingThreshold;

		static AdvancedChoices()
		{
			referenceFrames = CreateNumberList(0, 16, defaultNumber: 3);
			bFrames = CreateNumberList(0, 16, defaultNumber: 3);

			pyramidalBFrames = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.PyramidalBFrames_Off, Value = "none"},
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.PyramidalBFrames_Normal), IsDefault = true },
				new AdvancedChoice { Label = EncodingRes.PyramidalBFrames_Strict, Value = "strict"}
			};

			adaptiveBFrames = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.AdaptiveBFrames_Off, Value = "0" },
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.AdaptiveBFrames_Fast), Value = "1", IsDefault = true },
				new AdvancedChoice { Label = EncodingRes.AdaptiveBFrames_Optimal, Value = "2" }
			};

			directPrediction = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.DirectPrediction_None, Value = "none" },
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.DirectPrediction_Spatial), Value = "spatial", IsDefault = true },
				new AdvancedChoice { Label = EncodingRes.DirectPrediction_Temporal, Value = "temporal" },
				new AdvancedChoice { Label = EncodingRes.DirectPrediction_Automatic, Value = "auto" }
			};

			motionEstimationMethod = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.MotionEstimationMethod_Diamond, Value = "dia" },
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.MotionEstimationMethod_Hexagon), Value = "hex", IsDefault = true },
				new AdvancedChoice { Label = EncodingRes.MotionEstimationMethod_UnevenMultiHexagon, Value = "umh" },
				new AdvancedChoice { Label = EncodingRes.MotionEstimationMethod_Exhaustive, Value = "esa" },
				new AdvancedChoice { Label = EncodingRes.MotionEstimationMethod_TransformedExhaustive, Value = "tesa" },
			};

			subpixelMotionEstimation = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_0, Value = "0" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_1, Value = "1" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_2, Value = "2" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_3, Value = "3" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_4, Value = "4" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_5, Value = "5" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_6, Value = "6" },
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.SubpixelMotionEstimation_7), Value = "7", IsDefault = true },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_8, Value = "8" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_9, Value = "9" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_10, Value = "10" },
				new AdvancedChoice { Label = EncodingRes.SubpixelMotionEstimation_11, Value = "11" },
			};

			motionEstimationRange = CreateNumberList(4, 64, defaultNumber: 16);

			analysis = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.Analysis_None, Value = "none" },
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.Analysis_Some), IsDefault = true },
				new AdvancedChoice { Label = EncodingRes.Analysis_All, Value = "all" }
			};

			trellis = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = EncodingRes.Trellis_Off, Value = "0"},
				new AdvancedChoice { Label = string.Format(EncodingRes.DefaultFormat, EncodingRes.Trellis_EncodeOnly), Value = "1", IsDefault = true},
				new AdvancedChoice { Label = EncodingRes.Trellis_Always, Value = "2"}
			};

			deblockingStrength = CreateNumberList(-6, 6, defaultNumber: 0);
			deblockingThreshold = CreateNumberList(-6, 6, defaultNumber: 0);
		}

		#region Properties
		public static List<AdvancedChoice> ReferenceFrames
		{
			get
			{
				return referenceFrames;
			}
		}

		public static List<AdvancedChoice> BFrames
		{
			get
			{
				return bFrames;
			}
		}

		public static List<AdvancedChoice> PyramidalBFrames
		{
			get
			{
				return pyramidalBFrames;
			}
		}

		public static List<AdvancedChoice> AdaptiveBFrames
		{
			get
			{
				return adaptiveBFrames;
			}
		}

		public static List<AdvancedChoice> DirectPrediction
		{
			get
			{
				return directPrediction;
			}
		}

		public static List<AdvancedChoice> MotionEstimationMethod
		{
			get
			{
				return motionEstimationMethod;
			}
		}

		public static List<AdvancedChoice> SubpixelMotionEstimation
		{
			get
			{
				return subpixelMotionEstimation;
			}
		}

		public static List<AdvancedChoice> MotionEstimationRange
		{
			get
			{
				return motionEstimationRange;
			}
		}

		public static List<AdvancedChoice> Analysis
		{
			get
			{
				return analysis;
			}
		}

		public static List<AdvancedChoice> Trellis
		{
			get
			{
				return trellis;
			}
		}

		public static List<AdvancedChoice> DeblockingStrength
		{
			get
			{
				return deblockingStrength;
			}
		}

		public static List<AdvancedChoice> DeblockingThreshold
		{
			get
			{
				return deblockingThreshold;
			}
		}
		#endregion

		private static List<AdvancedChoice> CreateNumberList(int lower, int upper, int defaultNumber)
		{
			var list = new List<AdvancedChoice>();
			AddRange(list, lower, upper, defaultNumber);

			return list;
		}

		private static void AddRange(List<AdvancedChoice> list, int lower, int upper, int defaultNumber)
		{
			for (int i = lower; i <= upper; i++)
			{
				if (i == defaultNumber)
				{
					list.Add(new AdvancedChoice
					{
						IsDefault = true,
						Label = string.Format(EncodingRes.DefaultFormat, i),
						Value = i.ToString(CultureInfo.InvariantCulture)
					});
				}
				else
				{
					list.Add(new AdvancedChoice
					{
						Label = i.ToString(CultureInfo.CurrentCulture),
						Value = i.ToString(CultureInfo.InvariantCulture)
					});
				}
			}
		}
	}
}
