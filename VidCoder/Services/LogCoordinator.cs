using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.EventArgs;
using Microsoft.AnyContainer;
using VidCoder.Model;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.Services
{
	public class LogCoordinator
	{
		private IAppLogger mainLogger = StaticResolver.Resolve<IAppLogger>();

		public LogCoordinator()
		{
			if (!CustomConfig.UseWorkerProcess)
			{
				// The central logger always listens for messages when encoding locally
				HandBrakeUtils.MessageLogged += this.OnMessageLoggedLocal;
				HandBrakeUtils.ErrorLogged += this.OnErrorLoggedLocal;
			}
		}

		public SourceList<LogViewModel> Logs { get; } = new SourceList<LogViewModel>();

		public void AddLogger(IAppLogger logger, LogOperationType logOperationType, string operationPath)
		{
			this.Logs.Add(new LogViewModel(logger) { OperationPath = operationPath, OperationType = logOperationType });
		}

		private void OnMessageLoggedLocal(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.HandBrake,
				Text = e.Message
			};

			this.mainLogger.AddEntry(entry);
		}

		private void OnErrorLoggedLocal(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.HandBrake,
				Text = "ERROR: " + e.Message
			};

			this.mainLogger.AddEntry(entry);
		}
	}
}
