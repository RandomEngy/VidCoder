using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnyContainer;

namespace VidCoder.Services
{
	public class AppLoggerFactory
	{
		private readonly IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

		public IAppLogger ResolveJobLogger(string jobDescription)
		{
			if (CustomConfig.UseWorkerProcess)
			{
				return new AppLogger(this.logger, jobDescription);
			}
			else
			{
				return this.logger;
			}
		}

		public IAppLogger ResolveScanLogger()
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
