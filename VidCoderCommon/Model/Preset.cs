using Newtonsoft.Json;
using ReactiveUI;

namespace VidCoderCommon.Model
{
	[JsonObject]
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

		[JsonProperty]
		public string Name
		{
			get { return this.name; }
			set { this.RaiseAndSetIfChanged(ref this.name, value); }
		}

		/// <summary>
		/// Gets or sets the folder ID. 0 is the root "Custom" folder.
		/// </summary>
		[JsonProperty]
		public int FolderId { get; set; }

		[JsonProperty]
		public bool IsBuiltIn { get; set; }

		private bool isModified;

		[JsonProperty]
		public bool IsModified
		{
			get { return this.isModified; }
			set { this.RaiseAndSetIfChanged(ref this.isModified, value); }
		}

		[JsonProperty]
		public bool IsQueue { get; set; }

		private VCProfile encodingProfile;

		[JsonProperty]
		public VCProfile EncodingProfile
		{
			get { return this.encodingProfile; }
			set { this.RaiseAndSetIfChanged(ref this.encodingProfile, value); }
		}
	}
}
