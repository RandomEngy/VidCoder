
namespace VidCoder.Model
{
	using System.ComponentModel.DataAnnotations;
	using LocalResources;

	public enum AnamorphicCombo
	{
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_None")]
		None = 0,
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_Strict")]
		Strict,
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_Loose")]
		Loose,
		[Display(ResourceType = typeof(EnumsRes), Name = "Anamorphic_Custom")]
		Custom
	}
}
