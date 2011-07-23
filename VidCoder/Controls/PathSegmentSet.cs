using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Controls
{
	public class PathSegmentSet
	{
		public PathSegmentSet(int firstCutSegmentIndex, int lastCutSegmentIndex)
		{
			this.FirstCutSegmentIndex = firstCutSegmentIndex;
			this.LastCutSegmentIndex = lastCutSegmentIndex;
		}

		/// <summary>
		/// Gets or sets the index of the first segment to be clipped out.
		/// </summary>
		public int FirstCutSegmentIndex { get; set; }

		/// <summary>
		/// Gets or sets the index of the last segment to be clipped out.
		/// </summary>
		public int LastCutSegmentIndex { get; set; }
	}
}
