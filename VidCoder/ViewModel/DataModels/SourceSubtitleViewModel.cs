using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using HandBrake.Interop.Model;
using HandBrake.Interop.SourceData;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class SourceSubtitleViewModel : ViewModelBase
	{
		private SourceSubtitle subtitle;

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

		private bool selected;
		public bool Selected
		{
			get
			{
				return this.selected;
			}

			set
			{
				this.selected = value;
				this.RaisePropertyChanged(() => this.Selected);
			}
		}

		public string SubtitleName
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return "Foreign Audio Search";
				}

				return this.mainViewModel.SelectedTitle.Subtitles[this.TrackNumber - 1].Display;
			}
		}

		public int TrackNumber
		{
			get
			{
				return this.subtitle.TrackNumber;
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
				this.RaisePropertyChanged(() => this.Default);

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
				this.RaisePropertyChanged(() => this.Forced);
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
				this.RaisePropertyChanged(() => this.BurnedIn);

				if (value)
				{
					this.SubtitleDialogViewModel.ReportBurned(this);
				}

				this.SubtitleDialogViewModel.UpdateBoxes();
				this.SubtitleDialogViewModel.UpdateWarningVisibility();
			}
		}

		private RelayCommand removeSubtitleCommand;
		public RelayCommand RemoveSubtitleCommand
		{
			get
			{
				return this.removeSubtitleCommand ?? (this.removeSubtitleCommand = new RelayCommand(
					() =>
					{
						this.SubtitleDialogViewModel.RemoveSourceSubtitle(this);
					}));
			}
		}
	}
}
