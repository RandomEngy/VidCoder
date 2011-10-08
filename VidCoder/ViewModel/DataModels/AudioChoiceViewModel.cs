using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class AudioChoiceViewModel : ViewModelBase
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private int selectedTrack;

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		/// <summary>
		/// Gets or sets the 0-based index for the selected audio track.
		/// </summary>
		public int SelectedIndex
		{
			get
			{
				return this.selectedTrack;
			}

			set
			{
				this.selectedTrack = value;
				this.RaisePropertyChanged("SelectedIndex");

				EncodingViewModel encodingWindow = WindowManager.FindWindow(typeof(EncodingViewModel)) as EncodingViewModel;
				if (encodingWindow != null)
				{
					encodingWindow.NotifyAudioInputChanged();
				}
			}
		}

		private RelayCommand removeCommand;
		public RelayCommand RemoveCommand
		{
			get
			{
				return this.removeCommand ?? (this.removeCommand = new RelayCommand(() =>
					{
						this.mainViewModel.RemoveAudioChoice(this);
					}));
			}
		}
	}
}
