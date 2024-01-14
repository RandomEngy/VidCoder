using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Json.Scan;
using HandBrake.Interop.Interop.Json.Shared;
using VidCoder.Resources;
using VidCoder.ViewModel.DataModels;

namespace VidCoder;

    public static class ResolutionUtilities
    {
    public static List<InfoLineViewModel> GetResolutionInfoLines(SourceTitle title)
    {
		var previewLines = new List<InfoLineViewModel>();
	    if (title == null)
	    {
		    return previewLines;
	    }

	    string inputStorageResolutionString = title.Geometry.Width + " x " + title.Geometry.Height;
	    if (title.Geometry.PAR.Num == title.Geometry.PAR.Den)
	    {
		    previewLines.Add(new InfoLineViewModel(EncodingRes.ResolutionLabel, inputStorageResolutionString));
	    }
	    else
	    {
		    previewLines.Add(new InfoLineViewModel(EncodingRes.StorageResolutionLabel, inputStorageResolutionString));
		    previewLines.Add(new InfoLineViewModel(EncodingRes.PixelAspectRatioLabel, CreateParDisplayString(title.Geometry.PAR.Num, title.Geometry.PAR.Den)));

		    double pixelAspectRatio = ((double)title.Geometry.PAR.Num) / title.Geometry.PAR.Den;
		    double displayWidth = title.Geometry.Width * pixelAspectRatio;
		    int displayWidthRounded = (int)Math.Round(displayWidth);

		    string displayResolutionString = displayWidthRounded + " x " + title.Geometry.Height;

		    previewLines.Add(new InfoLineViewModel(EncodingRes.DisplayResolutionLabel, displayResolutionString));
	    }

	    return previewLines;
	}

    public static string CreateParDisplayString(int parWidth, int parHeight)
    {
	    double pixelAspectRatio = (double)parWidth / parHeight;
	    return pixelAspectRatio.ToString("F2") + " (" + parWidth + "/" + parHeight + ")";
    }
    }
