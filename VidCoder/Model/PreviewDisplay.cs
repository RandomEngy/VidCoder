using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
	using System.ComponentModel.DataAnnotations;
	using LocalResources;

	public enum PreviewDisplay
	{
		[Display(ResourceType = typeof(PreviewRes), Name = "PreviewDisplay_FitToWindow")]
		FitToWindow,

		[Display(ResourceType = typeof(PreviewRes), Name = "PreviewDisplay_OneToOne")]
		OneToOne,

		[Display(ResourceType = typeof(PreviewRes), Name = "PreviewDisplay_Corners")]
		Corners
	}
}
