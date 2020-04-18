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
				var newLogger = new AppLogger(this.allAppLogger, "Encode " + Path.GetFileName(outputPath));
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
				var newLogger = new AppLogger(this.allAppLogger, GetJobDescriptionFromSourcePath(sourcePath));
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
				var newLogger = new LocalScanAppLogger(this.allAppLogger, GetJobDescriptionFromSourcePath(sourcePath));
				this.logCoordinator.AddLogger(newLogger, LogOperationType.Scan, sourcePath);
				return newLogger;
			}
			else
			{
				return this.allAppLogger;
			}
		}

		private static string GetJobDescriptionFromSourcePath(string sourcePath)
		{
			try
			{
				string fileNameCleaned;
				if (FileUtilities.IsDirectory(sourcePath))
				{
					var info = new DirectoryInfo(sourcePath);
					fileNameCleaned = FileUtilities.CleanFileName(info.Name);
				}
				else
				{
					fileNameCleaned = Path.GetFileNameWithoutExtension(sourcePath);
				}

				return "Scan " + fileNameCleaned;
			}
			catch (Exception)
			{
				return "Scan";
			}
		}
	}
}
