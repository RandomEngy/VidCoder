using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Json.Shared;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoderCommon.Extensions;

namespace VidCoder.ViewModel.DataModels
{
    public class SourceTitleViewModel
    {
		public SourceTitleViewModel(SourceTitle title)
		{
			this.Title = title;
		}

		public SourceTitle Title { get; }

		public TimeSpan Duration => this.Title.Duration.ToSpan();

	    public int EstimatedFrames
	    {
		    get
		    {
				return (int)Math.Ceiling(this.Duration.TotalSeconds * this.Title.FrameRate.ToDouble());
		    }
	    }

	    public List<SourceChapter> ChapterList => this.Title.ChapterList;

	    public FrameRate FrameRate => this.Title.FrameRate;

	    public string VideoCodec => this.Title.VideoCodec;

	    public List<SourceSubtitleTrack> SubtitleList => this.Title.SubtitleList;

	    public List<SourceAudioTrack> AudioList => this.Title.AudioList;

	    public Geometry Geometry => this.Title.Geometry;

	    public List<int> Crop => this.Title.Crop;

	    public int Index => this.Title.Index;

		public override string ToString()
		{
			return this.Title.GetDisplayString();
		}
    }
}
