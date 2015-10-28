using System;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class AudioChoiceViewModel : ReactiveObject
	{
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();

		public AudioChoiceViewModel()
		{
			this.Remove = ReactiveCommand.Create();
			this.Remove.Subscribe(_ => this.RemoveImpl());
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		/// <summary>
		/// Gets or sets the 0-based index for the selected audio track.
		/// </summary>
		private int selectedIndex;
		public int SelectedIndex
		{
			get { return this.selectedIndex; }
			set { this.RaiseAndSetIfChanged(ref this.selectedIndex, value); }
		}

		public ReactiveCommand<object> Remove { get; }
		private void RemoveImpl()
		{
			this.mainViewModel.RemoveAudioChoice(this);
		}
	}
}
