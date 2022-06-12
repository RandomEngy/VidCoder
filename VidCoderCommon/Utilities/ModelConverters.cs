using HandBrake.Interop.Interop.HbLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon.Utilities
{
	public static class ModelConverters
	{
		public static hb_filter_ids VCDeinterlaceToHbFilter(VCDeinterlace deinterlaceType)
		{
			switch (deinterlaceType)
			{
				case VCDeinterlace.Decomb:
					return hb_filter_ids.HB_FILTER_DECOMB;
				case VCDeinterlace.Yadif:
					return hb_filter_ids.HB_FILTER_YADIF;
				case VCDeinterlace.Bwdif:
					return hb_filter_ids.HB_FILTER_BWDIF;
				default:
					throw new ArgumentOutOfRangeException(nameof(deinterlaceType), $"Could not find filter for deinterlace type {deinterlaceType}.");
			}
		}

		public static hb_filter_ids VCDenoiseToHbFilter(VCDenoise denoiseType)
		{
			switch (denoiseType)
			{
				case VCDenoise.hqdn3d:
					return hb_filter_ids.HB_FILTER_HQDN3D;
				case VCDenoise.NLMeans:
					return hb_filter_ids.HB_FILTER_NLMEANS;
				default:
					throw new ArgumentOutOfRangeException(nameof(denoiseType), $"Could not find filter for denoise type {denoiseType}.");
			}
		}

		public static hb_filter_ids VCSharpenToHbFilter(VCSharpen sharpenType)
		{
			switch (sharpenType)
			{
				case VCSharpen.UnSharp:
					return hb_filter_ids.HB_FILTER_UNSHARP;
				case VCSharpen.LapSharp:
					return hb_filter_ids.HB_FILTER_LAPSHARP;
				default:
					throw new ArgumentOutOfRangeException(nameof(sharpenType), $"Could not find filter for sharpen type {sharpenType}.");
			}
		}
	}
}
