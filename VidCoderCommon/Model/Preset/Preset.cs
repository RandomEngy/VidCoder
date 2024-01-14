using ReactiveUI;

namespace VidCoderCommon.Model;

public class Preset : ReactiveObject
{
	public void SetEncodingProfileSilent(VCProfile profile)
	{
		this.encodingProfile = profile;
	}

	public void RaiseEncodingProfile()
	{
		this.RaisePropertyChanged("EncodingProfile");
	}

	private string name;

	public string Name
	{
		get { return this.name; }
		set { this.RaiseAndSetIfChanged(ref this.name, value); }
	}

	/// <summary>
	/// Gets or sets the folder ID. 0 is the root "Custom" folder.
	/// </summary>
	public long FolderId { get; set; }

	public bool IsBuiltIn { get; set; }

	private bool isModified;

	public bool IsModified
	{
		get { return this.isModified; }
		set { this.RaiseAndSetIfChanged(ref this.isModified, value); }
	}

	public bool IsQueue { get; set; }

	private VCProfile encodingProfile;

	public VCProfile EncodingProfile
	{
		get { return this.encodingProfile; }
		set { this.RaiseAndSetIfChanged(ref this.encodingProfile, value); }
	}
}
