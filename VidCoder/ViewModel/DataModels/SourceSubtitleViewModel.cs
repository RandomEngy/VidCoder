﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Windows.Input;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class SourceSubtitleViewModel : ReactiveObject
	{
		private ChosenSourceSubtitle subtitle;

		private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();

		private SourceSubtitleTrack inputSubtitle; 

		public SourceSubtitleViewModel(MainViewModel mainViewModel, ChosenSourceSubtitle subtitle)
		{
			this.MainViewModel = mainViewModel;
			this.subtitle = subtitle;

			if (subtitle.TrackNumber != 0)
			{
				this.inputSubtitle = this.MainViewModel.SelectedTitle.SubtitleList[subtitle.TrackNumber - 1];
			}

			// CanPass
			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile.ContainerName, containerName =>
			{
				if (this.TrackNumber == 0)
				{
					return true;
				}

				return HandBrakeEncoderHelpers.SubtitleCanPassthrough(this.inputSubtitle.Source, HandBrakeEncoderHelpers.GetContainer(containerName).Id);
			}).ToProperty(this, x => x.CanPass, out this.canPass, scheduler: Scheduler.Immediate);

			// DefaultEnabled
			this.WhenAnyValue(x => x.BurnedIn, x => x.TrackNumber, x => x.MainViewModel.DefaultSubtitlesEnabled, (burnedIn, trackNumber, defaultSubtitlesEnabled) =>
			{
				return !burnedIn && (trackNumber == 0 || defaultSubtitlesEnabled);
			}).ToProperty(this, x => x.DefaultEnabled, out this.defaultEnabled);

			// BurnedInEnabled
			this.WhenAnyValue(x => x.CanPass, canPass =>
			{
				if (this.TrackNumber == 0)
				{
					return true;
				}

				if (!canPass)
				{
					return false;
				}

				if (!this.CanBurn)
				{
					return false;
				}

				return true;
			}).ToProperty(this, x => x.BurnedInEnabled, out this.burnedInEnabled);

			// When BurnedInEnabled changes, refresh to see if it should be updated to a predetermined value.
			this.WhenAnyValue(x => x.BurnedInEnabled).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(this.BurnedIn));
			});
		}

		public MainViewModel MainViewModel { get; }

		public List<SourceSubtitleTrack> InputSubtitles
		{
			get { return this.MainViewModel.SelectedTitle.SubtitleList; }
		}

		public ChosenSourceSubtitle Subtitle
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
				this.RaisePropertyChanged();
				this.MainViewModel.UpdateSourceSubtitleBoxes(this);
				this.MainViewModel.UpdateSubtitleWarningVisibility();
				this.MainViewModel.RefreshSubtitleSummary();
				this.UpdateButtonVisiblity();
			}
		}

		public bool IsForeignAudioSearch => this.TrackNumber == 0;

		public DateTimeOffset? LastMouseDownTime { get; set; }

		public override string ToString()
		{
			if (string.IsNullOrEmpty(this.Name))
			{
				return this.TrackSummary;
			}
			else if (this.TrackNumber == 0)
			{
				return SubtitleRes.ForeignAudioScan + " " + this.Name;
			}
			else
			{
				return this.TrackNumber + " " + this.Name + " - " + this.inputSubtitle.Language;
			}
		}

		public string TrackSummary
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return SubtitleRes.ForeignAudioScan;
				}

				return string.Format(
					"{0} {1}", 
					this.TrackNumber, 
					this.inputSubtitle.Language);
			}
		}

		public string Name
		{
			get => this.subtitle.Name ?? this.inputSubtitle?.Name;
			set
			{
				this.subtitle.Name = value;
				this.RaisePropertyChanged();
			}
		}

		public string SourceName => this.inputSubtitle?.Name;

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
				this.RaisePropertyChanged();

				if (value)
				{
					this.MainViewModel.ReportDefaultSubtitle(this);
				}
			}
		}

		private ObservableAsPropertyHelper<bool> defaultEnabled;
		public bool DefaultEnabled => this.defaultEnabled.Value;

		public bool ForcedOnly
		{
			get
			{
				if (this.TrackNumber > 0 && !HandBrakeEncoderHelpers.SubtitleCanSetForcedOnly(this.inputSubtitle.Source))
				{
					return false;
				}

				return this.subtitle.ForcedOnly;
			}

			set
			{
				this.subtitle.ForcedOnly = value;
				this.RaisePropertyChanged();
			}
		}

		public bool ForcedOnlyEnabled
		{
			get
			{
				return this.TrackNumber == 0 || HandBrakeEncoderHelpers.SubtitleCanSetForcedOnly(this.inputSubtitle.Source);
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
				this.RaisePropertyChanged();

				if (value)
				{
					this.Default = false;
					this.MainViewModel.ReportBurnedSubtitle(this);
				}

				this.MainViewModel.UpdateSourceSubtitleBoxes();
				this.MainViewModel.UpdateSubtitleWarningVisibility();
			}
		}

		private ObservableAsPropertyHelper<bool> burnedInEnabled;
		public bool BurnedInEnabled => this.burnedInEnabled.Value;

		public bool RemoveVisible
		{
			get
			{
				return this.MainViewModel.HasMultipleSourceSubtitleTracks(this.TrackNumber);
			}
		}

		public bool DuplicateVisible
		{
			get
			{
				return !this.MainViewModel.HasMultipleSourceSubtitleTracks(this.TrackNumber) && this.Selected;
			}
		}

		private ObservableAsPropertyHelper<bool> canPass;
		public bool CanPass
		{
			get { return this.canPass.Value; }
		}

		public bool CanBurn
		{
			get
			{
				if (this.TrackNumber == 0)
				{
					return true;
				}

				return HandBrakeEncoderHelpers.SubtitleCanBurn(this.inputSubtitle.Source);
			}
		}

		private ReactiveCommand<Unit, Unit> duplicateSubtitle;
		public ICommand DuplicateSubtitle
		{
			get
			{
				return this.duplicateSubtitle ?? (this.duplicateSubtitle = ReactiveCommand.Create(() =>
				{
					this.MainViewModel.DuplicateSourceSubtitle(this);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> removeSubtitle;
		public ICommand RemoveSubtitle
		{
			get
			{
				return this.removeSubtitle ?? (this.removeSubtitle = ReactiveCommand.Create(() =>
				{
					this.MainViewModel.RemoveSourceSubtitle(this);
				}));
			}
		}

		public void Deselect()
		{
			this.selected = false;
			this.RaisePropertyChanged(nameof(this.Selected));
		}

		public void UpdateButtonVisiblity()
		{
			this.RaisePropertyChanged(nameof(this.DuplicateVisible));
			this.RaisePropertyChanged(nameof(this.RemoveVisible));
		}
	}
}
