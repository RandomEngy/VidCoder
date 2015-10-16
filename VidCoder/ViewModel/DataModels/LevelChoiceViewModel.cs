using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class LevelChoiceViewModel : ReactiveObject
	{
		public LevelChoiceViewModel(string value)
		{
			this.Value = value;
			this.Display = value;
			this.isCompatible = true;
		}

		public LevelChoiceViewModel(string value, string display)
		{
			this.Value = value;
			this.Display = display;
			this.isCompatible = true;
		}

		public string Value { get; set; }

		public string Display { get; set; }

		private bool isCompatible;
		public bool IsCompatible
		{
			get { return this.isCompatible; }
			set { this.RaiseAndSetIfChanged(ref this.isCompatible, value); }
		}

	    public override string ToString()
	    {
	        return this.Display;
	    }
	}
}
