using HandBrake.Interop.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoder.Services;

public class QsvLoadBalancingService
{
	private List<int> qsvGpus = new List<int>();
	private int currentGpuIndex = -1;

	public QsvLoadBalancingService()
	{
		this.qsvGpus = HandBrakeEncoderHelpers.GetQsvAdaptorList();
	}

	public int GetQsvGpu(VCJob job)
	{
		if (!job.EncodingProfile.VideoEncoder.StartsWith("qsv_", StringComparison.Ordinal))
		{
			return -1;
		}

		if (!Config.UseWorkerProcess)
		{
			return -1;
		}

		if (Config.MaxSimultaneousEncodes == 1)
		{
			return -1;
		}

		this.currentGpuIndex++;

		return this.qsvGpus[this.currentGpuIndex % this.qsvGpus.Count];
	}
}
