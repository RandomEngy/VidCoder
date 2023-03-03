using HandBrake.Interop.Interop;
using Microsoft.AnyContainer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.Services;

public class HardwareResourceService
{
	private const string TotalPoolName = "Total";

	/// <summary>
	/// Global lock for all hardware resource actions. All operations should be fast so there should be minimal lock contention.
	/// </summary>
	private readonly object lockObject = new object();

	private readonly IDisposable simultaneousJobsSubscription;

	private readonly HardwarePool qsvPool;
	private readonly HardwarePool nvencPool;
	private readonly HardwarePool vcePool;
	private readonly HardwarePool mfPool;
	private readonly HardwarePool totalPool;

	private readonly IAppLogger appLogger = StaticResolver.Resolve<IAppLogger>();

	/// <summary>
	/// Pools representing DVD/Blu-ray drives. Key is the root directory.
	/// </summary>
	private readonly Dictionary<string, HardwarePool> discDrivePools = new Dictionary<string, HardwarePool>();

	public HardwareResourceService()
	{
		this.qsvPool = new HardwarePool("QSV", HandBrakeEncoderHelpers.GetQsvAdaptorList().Count() * 2); // Two instances per GPU
		this.nvencPool = new HardwarePool("NVEnc", 3);
		this.vcePool = new HardwarePool("VCE", 3);
		this.mfPool = new HardwarePool("MF", 1);
		this.totalPool = new HardwarePool(TotalPoolName, Config.MaxSimultaneousEncodes);

		this.simultaneousJobsSubscription = Config.Observables.MaxSimultaneousEncodes.Skip(1).Subscribe(maxSimultaneousEncodes =>
		{
			lock (this.lockObject)
			{
				this.totalPool.SlotCount = maxSimultaneousEncodes;
			}
		});
	}

	/// <summary>
	/// Try to acquire necessary hardware to encode.
	/// </summary>
	/// <param name="job">The job to start.</param>
	/// <returns>True if the hardware slots have been acquired and the encode can start.</returns>
	public bool TryAcquireSlot(EncodeJobViewModel job)
	{
		lock (this.lockObject)
		{
			// Get the required hardware.
			IList<HardwarePool> requiredHardwarePools = this.GetRequiredHardware(job);

			// Check if we can acquire a slot in all of our pools. If any are full, short-circuit with false
			foreach (HardwarePool hardwarePool in requiredHardwarePools)
			{
				if (!hardwarePool.CanAcquireSlot())
				{
					if (hardwarePool.Name != TotalPoolName)
					{
						this.appLogger.Log($"Could not acquire slot for hardware pool {hardwarePool.Name} on {job.Job.FinalOutputPath}. Will not start an additional simultaneous encode.");
					}

					return false;
				}
			}

			foreach (HardwarePool hardwarePool in requiredHardwarePools)
			{
				hardwarePool.AcquireSlot(job);
			}
		}

		return true;
	}

	/// <summary>
	/// Releases the hardware slots reserved for this job.
	/// </summary>
	/// <param name="job">The job that finished.</param>
	public void ReleaseSlot(EncodeJobViewModel job)
	{
		lock (this.lockObject)
		{
			// Get the required hardware.
			IList<HardwarePool> requiredHardwarePools = this.GetRequiredHardware(job);

			foreach (HardwarePool hardwarePool in requiredHardwarePools)
			{
				hardwarePool.ReleaseSlot(job);
			}
		}
	}

	/// <summary>
	/// Gets the required hardware to encode.
	/// </summary>
	/// <param name="job">The job to get the hardware for.</param>
	/// <returns>The list of required hardware pools.</returns>
	/// <remarks>This should always return the same on each call so the acquire/release match up.</remarks>
	private IList<HardwarePool> GetRequiredHardware(EncodeJobViewModel job)
	{
		// Start with total pool
		var result = new List<HardwarePool> { this.totalPool };

		// Add drive pool if needed for source reading. Trying to encode two jobs on the same disc at once makes both jobs really slow.
		DriveInformation driveInfo = job.VideoSourceMetadata?.DriveInfo;
		if (driveInfo != null)
		{
			string driveRootDirectory = driveInfo.RootDirectory;
			if (driveRootDirectory != null)
			{
				HardwarePool drivePool;
				if (!this.discDrivePools.TryGetValue(driveRootDirectory, out drivePool))
				{
					drivePool = new HardwarePool(driveRootDirectory, 1);
					this.discDrivePools.Add(driveRootDirectory, drivePool);
				}

				result.Add(drivePool);
			}
		}

		// Add hardware encoder pool
		string videoEncoder = job.Job.EncodingProfile.VideoEncoder;
		if (videoEncoder.StartsWith("qsv_", StringComparison.Ordinal))
		{
			result.Add(this.qsvPool);
		}
		else if (videoEncoder.StartsWith("nvenc_", StringComparison.Ordinal))
		{
			result.Add(this.nvencPool);
		}
		else if (videoEncoder.StartsWith("vce_", StringComparison.Ordinal))
		{
			result.Add(this.vcePool);
		}
		else if (videoEncoder.StartsWith("mf_", StringComparison.Ordinal))
		{
			result.Add(this.mfPool);
		}

		return result;
	}
}
