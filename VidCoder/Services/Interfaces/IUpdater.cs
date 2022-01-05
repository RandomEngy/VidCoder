using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services
{
	using Model;

	public interface IUpdater
	{
		void CheckUpdates();
		bool HandlePendingUpdate();
		bool PromptToApplyUpdate();
		void HandleUpdatedSettings(bool updatesEnabled);

		UpdateState State { get; }
		Version LatestVersion { get; }
		double UpdateDownloadProgressFraction { get; set; }
	}
}
