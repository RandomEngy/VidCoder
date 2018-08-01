using System;
using System.Collections.Generic;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Model;
using ReactiveUI;
using VidCoder.Model;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{

	public class SrtSubtitleViewModel : ReactiveObject
	{
		private SrtSubtitle subtitle;

		public SrtSubtitleViewModel(MainViewModel mainViewModel, SrtSubtitle subtitle)
		{
			this.MainViewModel = mainViewModel;
			this.subtitle = subtitle;
		}

		public MainViewModel MainViewModel { get; }

		public SrtSubtitle Subtitle
		{
			get
			{
				return this.subtitle;
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
				if (value != this.subtitle.Default)
				{
					this.subtitle.Default = value;
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

				return this.subtitle.BurnedIn;
			}

			set
			{
				this.subtitle.BurnedIn = value;
				this.RaisePropertyChanged();

				if (value)
				{
					this.MainViewModel.ReportBurnedSubtitle(this);
				}

				this.MainViewModel.UpdateSourceSubtitleBoxes();
				this.MainViewModel.UpdateSubtitleWarningVisibility();
			}
		}

		public bool BurnedInEnabled
		{
			get
			{
				return HandBrakeEncoderHelpers.CanBurnSrt;
			}
		}

		public string FileName
		{
			get
			{
				return this.subtitle.FileName;
			}

			set
			{
				this.subtitle.FileName = value;
				this.RaisePropertyChanged();
			}
		}

		public string LanguageCode
		{
			get
			{
				return this.subtitle.LanguageCode;
			}

			set
			{
				this.subtitle.LanguageCode = value;
				this.RaisePropertyChanged();
			}
		}

		public string CharacterCode
		{
			get
			{
				return this.subtitle.CharacterCode;
			}

			set
			{
				this.subtitle.CharacterCode = value;
				this.RaisePropertyChanged();
			}
		}

		public int Offset
		{
			get
			{
				return this.subtitle.Offset;
			}

			set
			{
				this.subtitle.Offset = value;
				this.RaisePropertyChanged();
			}
		}

		public IList<Language> Languages
		{
			get
			{
				return HandBrakeLanguagesHelper.AllLanguages;
			}
		}

		public IList<string> CharCodes
		{
			get
			{
				return CharCode.Codes;
			}
		}

		private ReactiveCommand removeSubtitle;
		public ReactiveCommand RemoveSubtitle
		{
			get
			{
				return this.removeSubtitle ?? (this.removeSubtitle = ReactiveCommand.Create(() =>
				{
					this.MainViewModel.RemoveSrtSubtitle(this);
				}));
			}
		}
	}
}
