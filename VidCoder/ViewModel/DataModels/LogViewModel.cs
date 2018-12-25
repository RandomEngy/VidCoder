using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel.DataModels
{
	public class LogViewModel
	{
		public LogViewModel(IAppLogger logger)
		{
			this.Logger = logger;
		}

		public LogOperationType OperationType { get; set; }

		public string OperationPath { get; set; }

		public IAppLogger Logger { get; }

		public string OperationTypeDisplay
		{
			get
			{
				if (this.OperationType == LogOperationType.Scan)
				{
					return EnumsRes.LogOperationType_Scan;
				}

				return EnumsRes.LogOperationType_Encode;
			}
		}
	}
}
