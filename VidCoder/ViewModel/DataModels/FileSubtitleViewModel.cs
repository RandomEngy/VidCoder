using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Windows.Input;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Model;
using ReactiveUI;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class FileSubtitleViewModel : ReactiveObject
	{
		public FileSubtitleViewModel(MainViewModel mainViewModel, FileSubtitle subtitle)
		{
			this.MainViewModel = mainViewModel;
			this.Subtitle = subtitle;
		}

		public MainViewModel MainViewModel { get; }

		public FileSubtitle Subtitle { get; }

		public bool Default
		{
			get => this.Subtitle.Default;
			set
			{
				if (value != this.Subtitle.Default)
				{
					this.Subtitle.Default = value;
					this.RaisePropertyChanged();

					if (value)
					{
						this.MainViewModel.ReportDefaultSubtitle(this);
					}
				}
			}
		}

		public bool BurnedIn
		{
			get
			{
				if (!BurnedInEnabled)
				{
					return false;
				}

				return this.Subtitle.BurnedIn;
			}

			set
			{
				this.Subtitle.BurnedIn = value;
				this.RaisePropertyChanged();

				if (value)
				{
					this.MainViewModel.ReportBurnedSubtitle(this);
				}

				this.MainViewModel.UpdateSourceSubtitleBoxes();
				this.MainViewModel.UpdateSubtitleWarningVisibility();
			}
		}

		public bool BurnedInEnabled => HandBrakeEncoderHelpers.CanBurnSrt;

		public override string ToString()
		{
			return Path.GetFileName(this.FileName);
		}

		public string FileName
		{
			get => this.Subtitle.FileName;
			set
			{
				this.Subtitle.FileName = value;
				this.RaisePropertyChanged();
			}
		}

		public string Name
		{
			get => this.Subtitle.Name;
			set
			{
				this.Subtitle.Name = value;
				this.RaisePropertyChanged();
			}
		}

		public string LanguageCode
		{
			get => this.Subtitle.LanguageCode;
			set
			{
				this.Subtitle.LanguageCode = value;
				this.RaisePropertyChanged();
			}
		}

		public string CharacterCode
		{
			get => this.Subtitle.CharacterCode;
			set
			{
				this.Subtitle.CharacterCode = value;
				this.RaisePropertyChanged();
			}
		}

		public int Offset
		{
			get => this.Subtitle.Offset;
			set
			{
				this.Subtitle.Offset = value;
				this.RaisePropertyChanged();
			}
		}

		public IList<Language> Languages => HandBrakeLanguagesHelper.AllLanguages;

		public IList<string> CharCodes => CharCode.Codes;

		private ReactiveCommand<Unit, Unit> removeSubtitle;
		public ICommand RemoveSubtitle
		{
			get
			{
				return this.removeSubtitle ?? (this.removeSubtitle = ReactiveCommand.Create(() =>
				{
					this.MainViewModel.RemoveFileSubtitle(this);
				}));
			}
		}
	}
}
