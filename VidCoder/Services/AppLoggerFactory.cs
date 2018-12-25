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
		private readonly IAppLogger logger = StaticResolver.Resolve<IAppLogger>();
		private readonly LogCoordinator logCoordinator = StaticResolver.Resolve<LogCoordinator>();

		public IAppLogger ResolveEncodeLogger(string outputPath)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				var newLogger = new AppLogger(this.logger, Path.GetFileName(outputPath));
				this.logCoordinator.AddLogger(newLogger, LogOperationType.Encode, outputPath);
				return newLogger;
			}
			else
			{
				return this.logger;
			}
		}

		public IAppLogger ResolveRemoteScanLogger(string sourcePath)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				var newLogger = new AppLogger(this.logger, "Scan");
				this.logCoordinator.AddLogger(newLogger, LogOperationType.Scan, sourcePath);
				return newLogger;
			}
			else
			{
				return this.logger;
			}
		}

		public IAppLogger ResolveLocalScanLogger(string sourcePath)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				return new LocalScanAppLogger(this.logger, "Scan");
			}
			else
			{
				return this.logger;
			}
		}
	}
}
