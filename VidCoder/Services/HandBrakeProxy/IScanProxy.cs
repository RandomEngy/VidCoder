using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services.HandBrakeProxy;

/// <summary>
/// Abstraction for dealing with either a worker process or local scanner.
/// </summary>
public interface IScanProxy : IDisposable
{
        /// <summary>
        /// Fires when the scan progress is updated.
        /// </summary>
    event EventHandler<EventArgs<float>> ScanProgress;

        /// <summary>
        /// Fires when a scan has completed.
        /// </summary>
        event EventHandler<EventArgs<string>> ScanCompleted;

        void StartScan(string path, IAppLogger logger);
}
