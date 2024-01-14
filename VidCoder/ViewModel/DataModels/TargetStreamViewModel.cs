﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel;

public class TargetStreamViewModel
{
	public string Text { get; set; }
	public string TrackDetails { get; set; }

	public bool HasTrackDetails
	{
		get
		{
			return this.TrackDetails != null;
		}
	}

	public override string ToString()
	{
		return this.Text;
	}
}
