using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services
{
	public class AutoChangeTracker
	{
		private readonly HashSet<Guid> pendingGuids;
		private readonly object sync = new object();

		public AutoChangeTracker()
		{
			this.pendingGuids = new HashSet<Guid>();
		}

		public bool OperationInProgress
		{
			get
			{
				lock (this.sync)
				{
					return this.pendingGuids.Count > 0;
				}
			}
		}

		public IDisposable TrackAutoChange()
		{
			Guid id = Guid.NewGuid();
			var change = new PendingAutoChange(this, id);

			lock (this.sync)
			{
				this.pendingGuids.Add(id);
			}

			return change;
		} 

		public void ReportChangeComplete(Guid id)
		{
			lock (this.sync)
			{
				this.pendingGuids.Remove(id);
			}
		}
	}
}
