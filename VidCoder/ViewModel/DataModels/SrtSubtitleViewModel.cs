using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using HandBrake.Interop.Model;
using System.Windows.Input;

namespace VidCoder.ViewModel
{
	public class SrtSubtitleViewModel : ViewModelBase
	{
		private SrtSubtitle subtitle;

		public SrtSubtitleViewModel(SubtitleDialogViewModel subtitleDialogViewModel, SrtSubtitle subtitle)
		{
			this.SubtitleDialogViewModel = subtitleDialogViewModel;
			this.subtitle = subtitle;
		}

		public SubtitleDialogViewModel SubtitleDialogViewModel { get; set; }

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
					this.RaisePropertyChanged(() => this.Default);

					if (value)
					{
						this.SubtitleDialogViewModel.ReportDefault(this);
					}
				}
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
				this.RaisePropertyChanged(() => this.FileName);
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
				this.RaisePropertyChanged(() => this.LanguageCode);
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
				this.RaisePropertyChanged(() => this.CharacterCode);
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
				this.RaisePropertyChanged(() => this.Offset);
			}
		}

		public IList<Language> Languages
		{
			get
			{
				return Language.Languages;
			}
		}

		public IList<string> CharCodes
		{
			get
			{
				return CharCode.Codes;
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
						this.SubtitleDialogViewModel.RemoveSrtSubtitle(this);
					}));
			}
		}
	}
}
