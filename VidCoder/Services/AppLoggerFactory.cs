using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using VidCoder.Model;

namespace VidCoder.Services
{
	public class AppLoggerFactory
	{
		private readonly IAppLogger allAppLogger = StaticResolver.Resolve<AllAppLogger>();
		private readonly LogCoordinator logCoordinator = StaticResolver.Resolve<LogCoordinator>();

		public IAppLogger ResolveEncodeLogger(string outputPath)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				var newLogger = new AppLogger(this.allAppLogger, Path.GetFileName(outputPath));
				this.logCoordinator.AddLogger(newLogger, LogOperationType.Encode, outputPath);
				return newLogger;
			}
			else
			{
				return this.allAppLogger;
			}
		}

		public IAppLogger ResolveRemoteScanLogger(string sourcePath)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				var newLogger = new AppLogger(this.allAppLogger, "Scan");
				this.logCoordinator.AddLogger(newLogger, LogOperationType.Scan, sourcePath);
				return newLogger;
			}
			else
			{
				return this.allAppLogger;
			}
		}

		public IAppLogger ResolveLocalScanLogger(string sourcePath)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				var newLogger = new LocalScanAppLogger(this.allAppLogger, "Scan");
				this.logCoordinator.AddLogger(newLogger, LogOperationType.Scan, sourcePath);
				return newLogger;
			}
			else
			{
				return this.allAppLogger;
			}
		}
	}
}
