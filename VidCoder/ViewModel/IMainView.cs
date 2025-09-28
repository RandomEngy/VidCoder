using System;
using System.Collections.Generic;
using VidCoder.Model;

namespace VidCoder.ViewModel;

public interface IMainView
{
	event EventHandler<RangeFocusEventArgs> RangeControlGotFocus;

	void SaveQueueColumns();

	void SaveCompletedColumns();

	void ApplyQueueColumns();

	void ApplyCompletedColumns();

	IList<EncodeJobViewModel> SelectedJobs { get; }

	void RefreshDiscMenuItems();

	void ResizeAudioColumns();

	double SourceAreaHeight { get; }

	void RefreshSummaryMaxSizes();

	void BringExternalSubtitlesIntoView();
}
