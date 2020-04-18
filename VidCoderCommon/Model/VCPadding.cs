using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Model
{
	public class VCPadding
	{
		public VCPadding()
		{
		}

		public VCPadding(VCPadding cropping)
		{
			this.Top = cropping.Top;
			this.Bottom = cropping.Bottom;
			this.Left = cropping.Left;
			this.Right = cropping.Right;
		}

		public VCPadding(int top, int bottom, int left, int right)
		{
			this.Top = top;
			this.Bottom = bottom;
			this.Left = left;
			this.Right = right;
		}

		public int Top { get; set; }

		public int Bottom { get; set; }

		public int Left { get; set; }

		public int Right { get; set; }

		public bool IsZero => this.Top == 0 && this.Bottom == 0 && this.Left == 0 && this.Right == 0;

		public override bool Equals(object obj)
		{
			var otherPadding = obj as VCPadding;
			if (otherPadding == null)
			{
				return false;
			}

			return this.Top == otherPadding.Top &&
			       this.Bottom == otherPadding.Bottom &&
			       this.Left == otherPadding.Left &&
			       this.Right == otherPadding.Right;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = this.Top;
				hashCode = (hashCode * 397) ^ this.Bottom;
				hashCode = (hashCode * 397) ^ this.Left;
				hashCode = (hashCode * 397) ^ this.Right;
				return hashCode;
			}
		}
	}
}
