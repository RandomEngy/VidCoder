using System;

namespace VidCoder.ViewModel;

public interface IPreviewView
{
	void RefreshImageSize();
	void RefreshViewModelFromMediaElement();
	void SeekToViewModelPosition(TimeSpan position);
}
