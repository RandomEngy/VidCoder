using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.ViewModel
{
	public interface ICompareView
	{
		void RefreshViewModelFromMediaElement();
		void SeekToViewModelPosition(TimeSpan position);
	}
}
