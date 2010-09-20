using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.SourceData;
using System.Windows.Input;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class AudioChoiceViewModel : ViewModelBase
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private int selectedTrack;
		private ICommand removeChoice;

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
				this.NotifyPropertyChanged("SelectedIndex");
			}
		}

		public ICommand RemoveChoice
		{
			get
			{
				if (this.removeChoice == null)
				{
					this.removeChoice = new RelayCommand(
						param =>
						{
							this.mainViewModel.RemoveAudioChoice(this);
						},
						param =>
						{
							return true;
						});
				}

				return this.removeChoice;
			}
		}
	}
}
