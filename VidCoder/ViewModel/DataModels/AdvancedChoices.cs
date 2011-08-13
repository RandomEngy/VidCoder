using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
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
				new AdvancedChoice { Label = "Off", Value = "none"},
				new AdvancedChoice { Label = "Normal (Default)", IsDefault = true },
				new AdvancedChoice { Label = "Strict", Value = "strict"}
			};

			adaptiveBFrames = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "Off", Value = "0" },
				new AdvancedChoice { Label = "Fast (Default)", Value = "1", IsDefault = true },
				new AdvancedChoice { Label = "Optimal", Value = "2" }
			};

			directPrediction = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "None", Value = "none" },
				new AdvancedChoice { Label = "Spatial (Default)", Value = "spatial", IsDefault = true },
				new AdvancedChoice { Label = "Temporal", Value = "temporal" },
				new AdvancedChoice { Label = "Automatic", Value = "auto" }
			};

			motionEstimationMethod = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "Diamond", Value = "dia" },
				new AdvancedChoice { Label = "Hexagon (Default)", Value = "hex", IsDefault = true },
				new AdvancedChoice { Label = "Uneven Multi-Hexagon", Value = "umh" },
				new AdvancedChoice { Label = "Exhaustive", Value = "esa" },
				new AdvancedChoice { Label = "Transformed Exhaustive", Value = "tesa" },
			};

			subpixelMotionEstimation = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "0: SAD, no subpel (super fast!)", Value = "0" },
				new AdvancedChoice { Label = "1: SAD, qpel", Value = "1" },
				new AdvancedChoice { Label = "2: SATD, qpel", Value = "2" },
				new AdvancedChoice { Label = "3: SATD, multi-qpel", Value = "3" },
				new AdvancedChoice { Label = "4: SATD, qpel on all", Value = "4" },
				new AdvancedChoice { Label = "5: SATD, multi-qpel on all", Value = "5" },
				new AdvancedChoice { Label = "6: RD in I/P-frames", Value = "6" },
				new AdvancedChoice { Label = "7: RD in all frames (Default)", Value = "7", IsDefault = true },
				new AdvancedChoice { Label = "8: RD refine in I/P-frames", Value = "8" },
				new AdvancedChoice { Label = "9: RD refine in all frames", Value = "9" },
				new AdvancedChoice { Label = "10: QPRD in all frames", Value = "10" },
				new AdvancedChoice { Label = "11: No early terminations in analysis", Value = "11" },
			};

			//subpixelMotionEstimation = CreateNumberList(0, 9, defaultNumber: 7);
			motionEstimationRange = CreateNumberList(4, 64, defaultNumber: 16);

			analysis = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "None", Value = "none" },
				new AdvancedChoice { Label = "Some (Default)", IsDefault = true },
				new AdvancedChoice { Label = "All", Value = "all" }
			};

			trellis = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "Off", Value = "0"},
				new AdvancedChoice { Label = "Encode Only (Default)", Value = "1", IsDefault = true},
				new AdvancedChoice { Label = "Always", Value = "2"}
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
						Label = i.ToString() + " (Default)",
						Value = i.ToString()
					});
				}
				else
				{
					list.Add(new AdvancedChoice
					{
						Label = i.ToString(),
						Value = i.ToString()
					});
				}
			}
		}
	}
}
