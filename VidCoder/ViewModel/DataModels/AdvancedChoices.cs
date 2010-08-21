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

			subpixelMotionEstimation = CreateNumberList(0, 9, defaultNumber: 7);
			motionEstimationRange = CreateNumberList(4, 64, defaultNumber: 16);

			analysis = new List<AdvancedChoice>
			{
				new AdvancedChoice { Label = "None", Value = "none" },
				new AdvancedChoice { Label = "Some (Default)", IsDefault = true },
				new AdvancedChoice { Label = "All", Value = "all" }
			};

			trellis = CreateNumberList(0, 2, defaultNumber: 1);
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
