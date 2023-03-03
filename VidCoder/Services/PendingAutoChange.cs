using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services;

public class PendingAutoChange : IDisposable
{
	private readonly AutoChangeTracker tracker;
	private readonly Guid id;

	public PendingAutoChange(AutoChangeTracker tracker, Guid id)
	{
		this.tracker = tracker;
		this.id = id;
	}

	public void Dispose()
	{
		this.tracker.ReportChangeComplete(this.id);
	}
}
