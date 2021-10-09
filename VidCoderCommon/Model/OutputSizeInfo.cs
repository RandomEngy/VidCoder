﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Json.Shared;

namespace VidCoderCommon.Model
{
	public class OutputSizeInfo
	{
		/// <summary>
		/// The width to scale to (after rotation).
		/// </summary>
		public int ScaleWidth { get; set; }

		/// <summary>
		/// The height to scale to (after rotation).
		/// </summary>
		public int ScaleHeight { get; set; }

		/// <summary>
		/// The final output width of the video.
		/// </summary>
		public int OutputWidth { get; set; }

		/// <summary>
		/// The final output height of the video.
		/// </summary>
		public int OutputHeight { get; set; }

		public PAR Par { get; set; }

		public VCPadding Padding { get; set; }

		/// <summary>
		/// The final picture width, post-rotation, scaling and not counting padding.
		/// </summary>
		public int PictureWidth
		{
			get
			{
				if (this.Padding == null)
				{
					return this.OutputWidth;
				}

				return this.OutputWidth - this.Padding.Left - this.Padding.Right;
			}
		}

		/// <summary>
		/// The final picture height, post-rotation, scaling and not counting padding.
		/// </summary>
		public int PictureHeight
		{
			get
			{
				if (this.Padding == null)
				{
					return this.OutputHeight;
				}

				return this.OutputHeight - this.Padding.Top - this.Padding.Bottom;
			}
		}

		public override bool Equals(object obj)
		{
			var otherOutputSize = obj as OutputSizeInfo;
			if (otherOutputSize == null)
			{
				return false;
			}

			return this.OutputWidth == otherOutputSize.OutputWidth &&
				   this.OutputHeight == otherOutputSize.OutputHeight &&
				   this.ScaleWidth == otherOutputSize.ScaleWidth &&
				   this.ScaleHeight == otherOutputSize.ScaleHeight &&
				   this.Par.Num == otherOutputSize.Par.Num &&
				   this.Par.Den == otherOutputSize.Par.Den &&
				   this.Padding == otherOutputSize.Padding;
		}

		public int DisplayWidth => (int)Math.Round(this.OutputWidth * ((double)this.Par.Num / this.Par.Den));

		public double OutputAspectRatio => ((double)this.OutputWidth / this.OutputHeight) * ((double)this.Par.Num / this.Par.Den);

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = this.ScaleWidth;
				hashCode = (hashCode * 397) ^ this.ScaleHeight;
				hashCode = (hashCode * 397) ^ this.OutputWidth;
				hashCode = (hashCode * 397) ^ this.OutputHeight;
				hashCode = (hashCode * 397) ^ this.Par.Num;
				hashCode = (hashCode * 397) ^ this.Par.Den;
				hashCode = (hashCode * 397) ^ (this.Padding?.GetHashCode() ?? 0);
				return hashCode;
			}
		}
	}
}
