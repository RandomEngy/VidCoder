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
		public static hb_filter_ids VCDenoiseToHbDenoise(VCDenoise denoiseType)
		{
			switch (denoiseType)
			{
				case VCDenoise.hqdn3d:
					return hb_filter_ids.HB_FILTER_HQDN3D;
				case VCDenoise.NLMeans:
					return hb_filter_ids.HB_FILTER_NLMEANS;
				case VCDenoise.ChromaSmooth:
					return hb_filter_ids.HB_FILTER_CHROMA_SMOOTH;
				default:
					throw new ArgumentOutOfRangeException(nameof(denoiseType), $"Could not find filter for denoise type {denoiseType}.");
			}
		}
	}
}
