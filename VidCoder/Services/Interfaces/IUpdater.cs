using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services;

using Model;

public interface IUpdater
{
	void CheckUpdates(bool isManualCheck);
	bool PromptToApplyUpdate();
	void HandleUpdatedSettings(bool updatesEnabled);

	UpdateState State { get; }
	Version LatestVersion { get; }
	int UpdateDownloadProgressPercent { get; set; }
}
