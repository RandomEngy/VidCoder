using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.ViewModel;

namespace VidCoder.Model;

public class JobCompletedEventArgs : EventArgs
{
	public JobCompletedEventArgs(EncodeJobViewModel jobViewModel, EncodeResultStatus? resultStatus = null)
	{
		JobViewModel = jobViewModel;
		ResultStatus = resultStatus;
	}

	public EncodeJobViewModel JobViewModel { get; }

	/// <summary>
	/// Valid when Reason is Finished.
	/// </summary>
	public EncodeResultStatus? ResultStatus { get; }
}
