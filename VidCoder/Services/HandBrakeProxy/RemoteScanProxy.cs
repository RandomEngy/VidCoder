using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoderCommon;

namespace VidCoder.Services.HandBrakeProxy
{
	public class RemoteScanProxy : RemoteProxyBase, IHandBrakeWorkerCallback, IScanProxy
	{
	    public event EventHandler<EventArgs<float>> ScanProgress;
	    public event EventHandler<EventArgs<string>> ScanCompleted;

	    private string result;

	    public void StartScan(string path, IAppLogger logger)
		{
			this.Logger = logger;

			this.StartOperation(channel =>
			{
				channel.StartScan(path);
			});
		}

        public void OnScanProgress(float fractionComplete)
        {
            this.ScanProgress?.Invoke(this, new EventArgs<float>(fractionComplete));
        }

        public void OnScanComplete(string scanJson)
        {
            this.result = scanJson;

            lock (this.ProcessLock)
            {
                this.EndOperation(scanJson == null);
            }
        }

	    protected override void OnOperationEnd(bool error)
		{
            this.ScanCompleted?.Invoke(this, new EventArgs<string>(this.result));
        }

        // TODO: Refactor so we don't need to implement these
		public override void StopAndWait()
		{
			throw new NotImplementedException();
		}

	    public void OnEncodeStarted()
	    {
	        throw new NotImplementedException();
	    }

	    public void OnEncodeProgress(float averageFrameRate, float currentFrameRate, TimeSpan estimatedTimeLeft, float fractionComplete, int passId, int pass, int passCount, string stateCode)
	    {
	        throw new NotImplementedException();
	    }

	    public void OnEncodeComplete(bool error)
	    {
	        throw new NotImplementedException();
	    }
	}
}
