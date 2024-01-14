using System.ComponentModel.DataAnnotations;
using VidCoder.Resources;

namespace VidCoder.Model;

public enum PreviewDisplay
{
	[Display(ResourceType = typeof(CommonRes), Name = "Default")]
	Default,

	[Display(ResourceType = typeof(PreviewRes), Name = "PreviewDisplay_FitToWindow")]
	FitToWindow,

	[Display(ResourceType = typeof(PreviewRes), Name = "PreviewDisplay_OneToOne")]
	OneToOne,

	[Display(ResourceType = typeof(PreviewRes), Name = "PreviewDisplay_Corners")]
	Corners
}
