using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services
{
	using Model;

	public interface IUpdater
	{
		event EventHandler<EventArgs<double>> UpdateDownloadProgress;
		event EventHandler<EventArgs> UpdateStateChanged;

		void CheckUpdates();
		bool HandlePendingUpdate();
		void PromptToApplyUpdate();
		void HandleUpdatedSettings(bool updatesEnabled);

		UpdateState State { get; }
		string LatestVersion { get; }
	}
}
