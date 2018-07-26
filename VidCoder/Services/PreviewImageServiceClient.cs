using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace VidCoder.Services
{
	/// <summary>
	/// Represents a client to the preview image service.
	/// </summary>
    public class PreviewImageServiceClient : ReactiveObject
    {
	    private int previewIndex;
	    public int PreviewIndex
	    {
		    get { return this.previewIndex; }
		    set { this.RaiseAndSetIfChanged(ref this.previewIndex, value); }
	    }
	}
}
