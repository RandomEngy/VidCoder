using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services
{
	public interface IUpdater
	{
		event EventHandler<EventArgs<double>> UpdateDownloadProgress;
		event EventHandler<EventArgs> UpdateDownloadCompleted;

		void CheckUpdates();
		bool HandlePendingUpdate();
		void PromptToApplyUpdate();
		void HandleUpdatedSettings(bool updatesEnabled);

		bool UpdateDownloading { get; }
		bool UpdateReady { get; }
		bool UpToDate { get; set; }
	}
}
