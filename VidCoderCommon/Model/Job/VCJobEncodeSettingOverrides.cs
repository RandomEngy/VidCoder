namespace VidCoderCommon.Model;

/// <summary>
/// Temporary encode setting overrides that apply on top of an encoding profile.
/// </summary>
public class VCJobEncodeSettingOverrides
{
	/// <summary>
	/// Override crop values when profile CroppingType is Automatic or Loose.
	/// Null when no cropping override is active.
	/// </summary>
	public VCCropping Cropping { get; set; }

	public bool HasAny => this.Cropping != null;
}
