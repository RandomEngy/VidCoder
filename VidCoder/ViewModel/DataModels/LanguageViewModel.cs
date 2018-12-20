using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Model;
using ReactiveUI;

namespace VidCoder.ViewModel.DataModels
{
	public class LanguageViewModel : ReactiveObject
	{
		private readonly PickerWindowViewModel pickerWindowViewModel;

		public LanguageViewModel(PickerWindowViewModel pickerWindowViewModel)
		{
			this.pickerWindowViewModel = pickerWindowViewModel;
		}

		private string code;
		public string Code
		{
			get { return this.code; }
			set { this.RaiseAndSetIfChanged(ref this.code, value); }
		}

		public PickerWindowViewModel PickerWindowViewModel => this.pickerWindowViewModel;

		private ReactiveCommand<Unit, Unit> removeAudioLanguage;
		public ICommand RemoveAudioLanguage
		{
			get
			{
				return this.removeAudioLanguage ?? (this.removeAudioLanguage = ReactiveCommand.Create(() =>
				{
					this.pickerWindowViewModel.RemoveAudioLanguage(this);
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> removeSubtitleLanguage;
		public ICommand RemoveSubtitleLanguage
		{
			get
			{
				return this.removeSubtitleLanguage ?? (this.removeSubtitleLanguage = ReactiveCommand.Create(() =>
				{
					this.pickerWindowViewModel.RemoveSubtitleLanguage(this);
				}));
			}
		}

		public IList<Language> Languages
		{
			get
			{
				return HandBrakeLanguagesHelper.AllLanguages;
			}
		}
	}
}
