using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using VidCoder.Services;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Model
{
	/// <summary>
	/// Tracks all properties needed for overall work tracking on the queue.
	/// </summary>
	public class JobWork
	{
		private readonly bool isTwoPass;
		private readonly bool hasScanPass;

		public JobWork(TimeSpan jobVideoLength, bool isTwoPass, bool hasScanPass)
		{
			this.JobVideoLength = jobVideoLength;
			this.isTwoPass = isTwoPass;
			this.hasScanPass = hasScanPass;

			this.Cost = jobVideoLength.TotalSeconds;
			if (isTwoPass)
			{
				this.Cost += jobVideoLength.TotalSeconds;
			}

			if (hasScanPass)
			{
				this.Cost += jobVideoLength.TotalSeconds / EncodeJobViewModel.SubtitleScanCostFactor;
			}
		}

		public TimeSpan JobVideoLength { get; }

		public double Cost { get; }

		public double CompletedWork { get; set; }
	}
}
