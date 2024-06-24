using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using VidCoder.Services;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Model;

/// <summary>
/// Tracks all properties needed for overall work tracking on the queue.
/// </summary>
public class JobWork
{
	private readonly bool isMultiPass;
	private readonly bool hasScanPass;

	public JobWork(TimeSpan jobVideoLength, bool isMultiPass, bool hasScanPass)
	{
		this.JobVideoLength = jobVideoLength;
		this.isMultiPass = isMultiPass;
		this.hasScanPass = hasScanPass;

		this.Cost = jobVideoLength.TotalSeconds;
		if (isMultiPass)
		{
			this.Cost += jobVideoLength.TotalSeconds;
		}

		if (hasScanPass)
		{
			this.Cost += jobVideoLength.TotalSeconds / EncodeJobViewModel.SubtitleScanCostFactor;
		}
	}

	public TimeSpan JobVideoLength { get; }

	/// <summary>
	/// The cost of the job, where the units are seconds of video to convert, multiplied by pass count.
	/// </summary>
	public double Cost { get; }

	public double CompletedWork { get; set; }
}
