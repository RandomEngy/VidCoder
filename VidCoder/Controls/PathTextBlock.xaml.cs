using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;

namespace VidCoder.Controls
{
	/// <summary>
	/// Interaction logic for PathTextBlock.xaml
	/// </summary>
	public partial class PathTextBlock : UserControl
	{
		private double? manualMaxWidth;

		public PathTextBlock()
		{
			InitializeComponent();
		}

		protected override Size MeasureOverride(Size constraint)
		{
			double widthConstraint = constraint.Width;
			if (this.manualMaxWidth != null)
			{
				widthConstraint = this.manualMaxWidth.Value;
			}

			string shortenedText = this.GetShortText(widthConstraint);
			this.textBlock.Text = shortenedText;
			this.UpdateToolTip();

			Size textSize = this.MeasureString(shortenedText);

			return new Size(Math.Min(constraint.Width, textSize.Width), Math.Min(constraint.Height, textSize.Height));
		}

		private void UpdateToolTip()
		{
			if (this.textBlock.Text == this.Text)
			{
				this.textBlock.ToolTip = null;
			}
			else
			{
				this.textBlock.ToolTip = this.Text;
			}
		}

		public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
			"Text",
			typeof(string),
			typeof(PathTextBlock),
			new PropertyMetadata(string.Empty, OnTextChanged));
		public string Text
		{
			get
			{
				return (string)GetValue(TextProperty);
			}

			set
			{
				SetValue(TextProperty, value);
			}
		}

		public void SetManualMaxWidth(double maxWidth)
		{
			this.manualMaxWidth = maxWidth;
            this.InvalidateMeasure();
		}

		private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
	    {
	        var block = dependencyObject as PathTextBlock;
            block.InvalidateMeasure();
	    }

		// Refreshes the TextBlock text based on the current text and maximum text width.
		private string GetShortText(double maxWidth)
		{
			string fullPath = this.Text;

			if (string.IsNullOrEmpty(fullPath))
			{
				return string.Empty;
			}

			if (this.MeasureStringWidth(fullPath) <= maxWidth)
			{
				return fullPath;
			}

			this.textBlock.ToolTip = fullPath;

			try
			{
				// Split the path into segments
				List<string> pathSegments = GetPathSegments(fullPath);

				// Find the greatest number of segments that falls under the maximum width
				int low = 0;
				int high = pathSegments.Count - 1;

				while (low < high)
				{
					int mid = (low + high) / 2;
					if ((high - low) % 2 != 0)
					{
						// When there are two valid midpoints, pick the higher one. This allows our
						// algorithm to terminate gracefully.
						mid++;
					}

					double stringWidth = this.MeasureStringWidth(ConstructShortenedPath(pathSegments, GetSegmentSet(pathSegments.Count, mid), "..."));
					if (stringWidth > maxWidth)
					{
						// Midpoint is too large. Restrict search range to less than it.
						high = mid - 1;
					}
					else
					{
						low = mid;
					}
				}

				int numSegments = low;

				if (numSegments > 0)
				{
					return ConstructShortenedPath(pathSegments, GetSegmentSet(pathSegments.Count, numSegments), "...");
				}

				// Settle for a subset of the file name.
				// We'll show the last part of the name; we find the index to start from.
				string lastSegment = pathSegments[pathSegments.Count - 1];
				low = 0;
				high = lastSegment.Length - 1;

				while (low < high)
				{
					int mid = (low + high) / 2;

					double stringWidth = this.MeasureStringWidth("..." + lastSegment.Substring(mid));
					if (stringWidth > maxWidth)
					{
						// We are too far to the left (string is too big).
						// Move the search area to the right.
						low = mid + 1;
					}
					else
					{
						high = mid;
					}
				}

				int cutoffIndex = low;
				return "..." + lastSegment.Substring(cutoffIndex);
			}
			catch
			{
				return fullPath;
			}
		}

		private static List<string> GetPathSegments(string path)
		{
			string cleanPath;
			string prefix;
			if (path.StartsWith(@"\\"))
			{
				prefix = @"\\";
				cleanPath = path.Substring(2);
			}
			else if (path.Substring(1, 2) == @":\")
			{
				prefix = path.Substring(0, 3);
				cleanPath = path.Substring(3);
			}
			else
			{
				throw new ArgumentException("Unrecognized path format.", "path");
			}

			string[] segments = cleanPath.Split('\\');
			var result = new List<string>();
			result.Add(prefix + segments[0]);

			for (int i = 1; i < segments.Length; i++)
			{
				result.Add(segments[i]);
			}

			return result;
		}

		// Assumes desired segments < total segments
		private static PathSegmentSet GetSegmentSet(int totalSegments, int desiredSegments)
		{
			bool evenSegmentTotal = totalSegments % 2 == 0;
			int cutSegments = totalSegments - desiredSegments;

			int endLeft, startRight;

			if (evenSegmentTotal)
			{
				endLeft = totalSegments / 2 - 1;
				startRight = totalSegments / 2;
			}
			else
			{
				endLeft = totalSegments / 2 - 1;
				startRight = totalSegments / 2 + 1;
			}

			int cutSegmentsLeft, cutSegmentsRight;
			int sideCutSegments = cutSegments;
			if (!evenSegmentTotal)
			{
				sideCutSegments--;
			}

			if (sideCutSegments % 2 == 0)
			{
				cutSegmentsLeft = sideCutSegments / 2;
				cutSegmentsRight = sideCutSegments / 2;
			}
			else
			{
				cutSegmentsLeft = sideCutSegments / 2 + 1;
				cutSegmentsRight = sideCutSegments / 2;
			}

			return new PathSegmentSet(endLeft - cutSegmentsLeft + 1, startRight + cutSegmentsRight - 1);
		}

		private static string ConstructShortenedPath(List<string> pathSegments, PathSegmentSet segmentSet, string middleString)
		{
			var builder = new StringBuilder();
			for (int i = 0; i < segmentSet.FirstCutSegmentIndex; i++)
			{
				builder.Append(pathSegments[i]);
				builder.Append(@"\");
			}

			builder.Append(middleString);

			for (int i = segmentSet.LastCutSegmentIndex + 1; i < pathSegments.Count; i++)
			{
				builder.Append(@"\");
				builder.Append(pathSegments[i]);
			}

			return builder.ToString();
		}

		private double MeasureStringWidth(string candidate)
		{
			return this.MeasureString(candidate).Width;
		}

		private Size MeasureString(string candidate)
		{
			var formattedText = new FormattedText(
				candidate,
				CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight,
				new Typeface(this.textBlock.FontFamily, this.textBlock.FontStyle, this.textBlock.FontWeight, this.textBlock.FontStretch),
				this.textBlock.FontSize,
				Brushes.Black,
				new NumberSubstitution(),
				TextFormattingMode.Display,
				1);

			return new Size(formattedText.Width, formattedText.Height);
		}
	}
}
