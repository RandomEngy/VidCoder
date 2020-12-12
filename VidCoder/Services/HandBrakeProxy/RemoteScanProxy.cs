using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon;
using VidCoderCommon.Model;

namespace VidCoder.Services.HandBrakeProxy
{
	public class RemoteScanProxy : RemoteProxyBase<IHandBrakeScanWorker, IHandBrakeScanWorkerCallback>, IScanProxy, IHandBrakeScanWorkerCallback
	{
	    public event EventHandler<EventArgs<float>> ScanProgress;
	    public event EventHandler<EventArgs<string>> ScanCompleted;

	    private string result;

	    public async void StartScan(string path, IAppLogger logger)
		{
			this.Logger = logger;

			await this.RunOperationAsync(worker => worker.StartScan(path));
		}

        public void OnScanProgress(float fractionComplete)
        {
            this.ScanProgress?.Invoke(this, new EventArgs<float>(fractionComplete));
        }

        public void OnScanComplete(string scanJson)
        {
            this.result = scanJson;

			Task.Run(async () =>
			{
				await this.ProcessLock.WaitAsync().ConfigureAwait(false);
				try
				{
					this.EndOperation(scanJson == null ? VCEncodeResultCode.ErrorUnknown : VCEncodeResultCode.Succeeded);
				}
				finally
				{
					this.ProcessLock.Release();
				}
			});
        }

		protected override HandBrakeWorkerAction Action => HandBrakeWorkerAction.Scan;

		protected override IHandBrakeScanWorkerCallback CallbackInstance => this;

		protected override void OnOperationEnd(VCEncodeResultCode result)
		{
            this.ScanCompleted?.Invoke(this, new EventArgs<string>(this.result));
        }
	}
}
