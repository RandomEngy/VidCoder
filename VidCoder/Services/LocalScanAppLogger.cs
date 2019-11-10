using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.EventArgs;
using VidCoder.Model;

namespace VidCoder.Services
{
	public class LocalScanAppLogger : AppLogger
	{
		public LocalScanAppLogger(IAppLogger parent, string baseFileName) 
			: base(parent, baseFileName)
		{
			HandBrakeUtils.MessageLogged += this.OnMessageLoggedLocal;
			HandBrakeUtils.ErrorLogged += this.OnErrorLoggedLocal;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				HandBrakeUtils.MessageLogged -= this.OnMessageLoggedLocal;
				HandBrakeUtils.ErrorLogged -= this.OnErrorLoggedLocal;
			}
		}

		private void OnMessageLoggedLocal(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.HandBrake,
				Text = e.Message
			};

			this.AddEntry(entry);
		}

		private void OnErrorLoggedLocal(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.HandBrake,
				Text = "ERROR: " + e.Message
			};

			this.AddEntry(entry);
		}
	}
}
