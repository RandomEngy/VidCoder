using System.ComponentModel.DataAnnotations;
using VidCoder.Resources;

namespace VidCoder.Model;

public enum TitleType
{
	[Display(ResourceType = typeof(EnumsRes), Name = "TitleType_Dvd")]
	Dvd,

	[Display(ResourceType = typeof(EnumsRes), Name = "TitleType_Bluray")]
	Bluray,

	[Display(ResourceType = typeof(EnumsRes), Name = "TitleType_Stream")]
	Stream,

	[Display(ResourceType = typeof(EnumsRes), Name = "TitleType_Stream")]
	FFStream,
}
