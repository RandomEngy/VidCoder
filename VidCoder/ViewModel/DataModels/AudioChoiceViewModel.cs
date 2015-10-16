using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using ReactiveUI;
using VidCoder.Messages;

namespace VidCoder.ViewModel
{
	public class AudioChoiceViewModel : ReactiveObject
	{
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
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
				this.RaisePropertyChanged();

				Messenger.Default.Send(new AudioInputChangedMessage());
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
