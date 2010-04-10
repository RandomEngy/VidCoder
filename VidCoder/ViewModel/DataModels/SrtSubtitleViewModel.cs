using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using System.Windows.Input;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
    public class SrtSubtitleViewModel : ViewModelBase
    {
        private SrtSubtitle subtitle;
        private ICommand removeSubtitle;

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
                this.subtitle.Default = value;
                this.NotifyPropertyChanged("Default");

                this.SubtitleDialogViewModel.ReportDefault(this);
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
                this.NotifyPropertyChanged("FileName");
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
                this.NotifyPropertyChanged("LanguageCode");
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
                this.NotifyPropertyChanged("CharacterCode");
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
                this.NotifyPropertyChanged("Offset");
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

        public ICommand RemoveSubtitle
        {
            get
            {
                if (this.removeSubtitle == null)
                {
                    this.removeSubtitle = new RelayCommand(
                        param =>
                        {
                            this.SubtitleDialogViewModel.RemoveSrtSubtitle(this);
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
