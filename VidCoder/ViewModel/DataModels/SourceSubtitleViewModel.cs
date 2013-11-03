using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using HandBrake.Interop.Model;
using HandBrake.Interop.SourceData;

namespace VidCoder.ViewModel
{
	using GalaSoft.MvvmLight.Messaging;
	using HandBrake.Interop.Model.Encoding;
	using Messages;
	using Resources;

	public class SourceSubtitleViewModel : ViewModelBase
	{
		private SourceSubtitle subtitle;

		private MainViewModel mainViewModel = Ioc.Container.GetInstance<MainViewModel>();

		public SourceSubtitleViewModel(SubtitleDialogViewModel subtitleDialogViewModel, SourceSubtitle subtitle)
		{
			this.SubtitleDialogViewModel = subtitleDialogViewModel;
			this.subtitle = subtitle;

			MessengerInstance.Register<ContainerChangedMessage>(this,
				m =>
				{
					this.RefreshBoxes();
				});
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
				this.SubtitleDialogViewModel.UpdateBoxes(this);
				this.SubtitleDialogViewModel.UpdateWarningVisibility();
			}
		}

		public string SubtitleName
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return SubtitleRes.ForeignAudioSearch;
				}

				return this.InputSubtitle.Display;
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

		public bool ForcedOnly
		{
			get
			{
				if (this.TrackNumber > 0 && !this.InputSubtitle.CanSetForcedOnly)
				{
					return false;
				}

				return this.subtitle.Forced;
			}

			set
			{
				this.subtitle.Forced = value;
				this.RaisePropertyChanged(() => this.ForcedOnly);
			}
		}

		public bool ForcedOnlyEnabled
		{
			get
			{
				return this.TrackNumber == 0 || this.InputSubtitle.CanSetForcedOnly;
			}
		}

		public bool BurnedIn
		{
			get
			{
				if (!this.CanPass)
				{
					return true;
				}

				if (!this.CanBurn)
				{
					return false;
				}

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

		public bool BurnedInEnabled
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return true;
				}

				if (!this.CanPass)
				{
					return false;
				}

				if (!this.CanBurn)
				{
					return false;
				}

				return true;
			}
		}

		public bool RemoveVisible
		{
			get
			{
				return this.SubtitleDialogViewModel.HasMultipleSourceTracks(this.TrackNumber);
			}
		}

		public bool DuplicateVisible
		{
			get
			{
				return !this.SubtitleDialogViewModel.HasMultipleSourceTracks(this.TrackNumber);
			}
		}

		public bool CanPass
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return true;
				}

				return this.InputSubtitle.CanPass(this.SubtitleDialogViewModel.Container.Id);
			}
		}

		public bool CanBurn
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return true;
				}

				return this.InputSubtitle.CanBurn;
			}
		}

		private Subtitle InputSubtitle
		{
			get
			{
				return this.InputSubtitles[this.TrackNumber - 1];
			}
		}

		private RelayCommand duplicateSubtitleCommand;
		public RelayCommand DuplicateSubtitleCommand
		{
			get
			{
				return this.duplicateSubtitleCommand ?? (this.duplicateSubtitleCommand = new RelayCommand(() =>
					{
						this.SubtitleDialogViewModel.DuplicateSourceSubtitle(this);
					}));
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

		public void Deselect()
		{
			this.selected = false;
			this.RaisePropertyChanged(() => this.Selected);
		}

		public void UpdateButtonVisiblity()
		{
			this.RaisePropertyChanged(() => this.DuplicateVisible);
			this.RaisePropertyChanged(() => this.RemoveVisible);
		}

		private void RefreshBoxes()
		{
			this.RaisePropertyChanged(() => this.BurnedIn);
			this.RaisePropertyChanged(() => this.BurnedInEnabled);
		}
	}
}
