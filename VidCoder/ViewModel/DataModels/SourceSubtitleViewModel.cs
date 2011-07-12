using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Model;
using HandBrake.Interop.SourceData;
using VidCoder.Model;
using System.Windows.Input;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class SourceSubtitleViewModel : ViewModelBase
	{
		private SourceSubtitle subtitle;
		private ICommand removeSubtitle;

		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();

		public SourceSubtitleViewModel(SubtitleDialogViewModel subtitleDialogViewModel, SourceSubtitle subtitle)
		{
			this.SubtitleDialogViewModel = subtitleDialogViewModel;
			this.subtitle = subtitle;
		}

		public SubtitleDialogViewModel SubtitleDialogViewModel { get; set; }

		public List<Subtitle> InputSubtitles
		{
			get { return this.mainViewModel.SelectedTitle.Subtitles; }
		}

		public SourceSubtitle Subtitle
		{
			get
			{
				return this.subtitle;
			}
		}

		public string SubtitleName
		{
			get
			{
				return this.SubtitleDialogViewModel.InputTrackChoices[this.TrackNumber];
			}
		}

		public int TrackNumber
		{
			get
			{
				return this.subtitle.TrackNumber;
			}

			set
			{
				this.subtitle.TrackNumber = value;
				this.NotifyPropertyChanged("SubtitleName");
				this.NotifyPropertyChanged("TrackNumber");
				this.SubtitleDialogViewModel.UpdateWarningVisibility();
			}
		}

		public bool Default
		{
			get
			{
				return this.subtitle.Default;
			}

			set
			{
				this.subtitle.Default = value;
				this.NotifyPropertyChanged("Default");

				if (value)
				{
					this.SubtitleDialogViewModel.ReportDefault(this);
				}
			}
		}

		public bool Forced
		{
			get
			{
				return this.subtitle.Forced;
			}

			set
			{
				this.subtitle.Forced = value;
				this.NotifyPropertyChanged("Forced");
			}
		}

		public bool BurnedIn
		{
			get
			{
				return this.subtitle.BurnedIn;
			}

			set
			{
				this.subtitle.BurnedIn = value;
				this.NotifyPropertyChanged("BurnedIn");

				if (value)
				{
					this.SubtitleDialogViewModel.ReportBurned(this);
				}

				this.SubtitleDialogViewModel.UpdateBoxes();
				this.SubtitleDialogViewModel.UpdateWarningVisibility();
			}
		}

		public ICommand RemoveSubtitle
		{
			get
			{
				if (this.removeSubtitle == null)
				{
					this.removeSubtitle = new RelayCommand(
						param =>
						{
							this.SubtitleDialogViewModel.RemoveSourceSubtitle(this);
						},
						param =>
						{
							return true;
						});
				}

				return this.removeSubtitle;
			}
		}
	}
}
