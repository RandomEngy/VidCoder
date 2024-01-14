using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using PipeMethodCalls;
using VidCoderCommon;

namespace VidCoderWorker;

public class HandBrakeScanWorker : HandBrakeWorkerBase<IHandBrakeScanWorkerCallback>, IHandBrakeScanWorker
{
	public HandBrakeScanWorker(IPipeInvoker<IHandBrakeScanWorkerCallback> callback) 
		: base(callback)
	{
	}

	public void StartScan(string path)
	{
		this.Instance = new HandBrakeInstance();
		this.Instance.Initialize(this.PassedVerbosity, noHardware: false);
		this.Instance.ScanProgress += (o, e) =>
		{
			this.MakeOneWayCallback(c => c.OnScanProgress((float)e.Progress));
		};
		this.Instance.ScanCompleted += async (o, e) =>
		{
			try
			{
				await this.CallbackInvoker.InvokeAsync(c => c.OnScanComplete(this.Instance.TitlesJson));
			}
			catch (Exception exception)
			{
				WorkerErrorLogger.LogError("Got exception when reporting completion: " + exception, isError: true);
			}
			finally
			{
				this.Instance.Dispose();
			}
		};

		this.Instance.StartScan(
			paths: new List<string> { path },
			previewCount: this.PassedPreviewCount,
			minDuration: TimeSpan.FromSeconds(this.PassedMinTitleDurationSeconds),
			titleIndex: 0,
			excludedExtensions: new List<string>(),
			hwDecode: 0);
	}
}
