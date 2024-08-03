using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using ReactiveUI;
using VidCoder.Extensions;

namespace VidCoder.Services.HandBrakeProxy;

public class LocalScanProxy : IScanProxy
{
    public event EventHandler<EventArgs<float>> ScanProgress;
    public event EventHandler<EventArgs<string>> ScanCompleted;

	public void StartScan(string path, IAppLogger logger)
    {
        var onDemandInstance = new HandBrakeInstance();
        onDemandInstance.Initialize(Config.LogVerbosity, noHardware: false);
        onDemandInstance.ScanProgress += (o, e) =>
        {
            this.ScanProgress?.Invoke(this, new EventArgs<float>((float)e.Progress));
        };
        onDemandInstance.ScanCompleted += (o, e) =>
        {
            this.ScanCompleted?.Invoke(this, new EventArgs<string>(onDemandInstance.TitlesJson));
            onDemandInstance.Dispose();
        };

        onDemandInstance.StartScan(
			paths: new List<string> { path },
			previewCount: Config.PreviewCount,
			minDuration: TimeSpan.FromSeconds(Config.MinimumTitleLengthSeconds),
			titleIndex: 0,
			excludedExtensions: new List<string>(),
			hwDecode: 0,
			keepDuplicateTitles: false);
    }

	public void Dispose()
	{
	}
}
