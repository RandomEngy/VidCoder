using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.ViewModel;

namespace VidCoder.Model
{
	/// <summary>
	/// Tracks a pool of hardware than can support a limited number of simultaneous encodes.
	/// </summary>
	public class HardwarePool
	{
		/// <summary>
		/// The jobs that are currently using this hardware.
		/// </summary>
		private readonly List<EncodeJobViewModel> jobs = new List<EncodeJobViewModel>();

		public HardwarePool(string name, int slotCount)
		{
			this.Name = name;
			this.SlotCount = slotCount;
		}

		public string Name { get; }

		public int SlotCount { get; set; }

		/// <summary>
		/// Returns true if we can acquire the hardware slot.
		/// </summary>
		/// <returns>True if there is an open slot.</returns>
		public bool CanAcquireSlot()
		{
			return jobs.Count < this.SlotCount;
		}

		/// <summary>
		/// Tries to acquire a slot to use this hardware.
		/// </summary>
		/// <param name="job">The job trying to acquire the slot.</param>
		/// <returns>True if the slot was acquired.</returns>
		/// <remarks>This method is not thread-safe. Locking needs to happen at the HardwareResourceService level.</remarks>
		public void AcquireSlot(EncodeJobViewModel job)
		{
			if (jobs.Count >= this.SlotCount)
			{
				throw new InvalidOperationException($"Hardware pool {this.Name} has no slots available for {job.Job.FinalOutputPath} .");
			}

			if (jobs.Contains(job))
			{
				throw new InvalidOperationException($"Hardware pool {this.Name} already has encode job {job.Job.FinalOutputPath} .");
			}

			this.jobs.Add(job);
		}

		/// <summary>
		/// Releases the hardware slot 
		/// </summary>
		/// <param name="job">The job to release.</param>
		/// <remarks>This method is not thread-safe. Locking needs to happen at the HardwareResourceService level.</remarks>
		public void ReleaseSlot(EncodeJobViewModel job)
		{
			if (!this.jobs.Remove(job))
			{
				throw new InvalidOperationException($"Hardware pool {this.Name} cannot release slot for {job.Job.FinalOutputPath} , it was never acquired.");
			}
		}
	}
}
