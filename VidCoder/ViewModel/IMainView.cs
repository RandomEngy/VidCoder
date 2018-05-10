using System;
using System.Collections.Generic;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	public interface IMainView
	{
		event EventHandler<RangeFocusEventArgs> RangeControlGotFocus;

		void SaveQueueColumns();

		void SaveCompletedColumnWidths();

		void ApplyQueueColumns();

		IList<EncodeJobViewModel> SelectedJobs { get; }

		void RefreshDiscMenuItems();
	}
}
