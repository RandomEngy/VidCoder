using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.ViewModel
{
	public interface IPickerWindowView
	{
		void ScrollAudioSectionIntoView();

		void ScrollSubtitlesSectionIntoView();

		void ScrollDestinationSectionIntoView();

		void FocusAudioTrackName(int index);

		void FocusSubtitleTrackName(int index);
	}
}
