using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Model;
using ReactiveUI;

namespace VidCoder.ViewModel.DataModels
{
	public class LanguageViewModel : ReactiveObject
	{
		private readonly PickerWindowViewModel pickerWindowViewModel;

		public LanguageViewModel(PickerWindowViewModel pickerWindowViewModel)
		{
			this.pickerWindowViewModel = pickerWindowViewModel;

			this.RemoveAudioLanguage = ReactiveCommand.Create();
			this.RemoveAudioLanguage.Subscribe(_ => this.RemoveAudioLanguageImpl());

			this.RemoveSubtitleLanguage = ReactiveCommand.Create();
			this.RemoveSubtitleLanguage.Subscribe(_ => this.RemoveSubtitleLanguageImpl());
		}

		private string code;
		public string Code
		{
			get { return this.code; }
			set { this.RaiseAndSetIfChanged(ref this.code, value); }
		}

		public PickerWindowViewModel PickerWindowViewModel => this.pickerWindowViewModel;

		public ReactiveCommand<object> RemoveAudioLanguage { get; }
		private void RemoveAudioLanguageImpl()
		{
			this.pickerWindowViewModel.RemoveAudioLanguage(this);
		}

		public ReactiveCommand<object> RemoveSubtitleLanguage { get; }
		private void RemoveSubtitleLanguageImpl()
		{
			this.pickerWindowViewModel.RemoveSubtitleLanguage(this);
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
