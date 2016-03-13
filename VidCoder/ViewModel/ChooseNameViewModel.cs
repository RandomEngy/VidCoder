using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using VidCoder.Model;
using VidCoder.Resources;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class ChooseNameViewModel : OkCancelDialogViewModel
	{
        private IEnumerable<string> existingNames;

		public ChooseNameViewModel(string message, IEnumerable<string> existingNames)
		{
            this.existingNames = existingNames;

		    this.Title = MiscRes.NameDialogTitle;
		    this.Subtitle = message;

			this.WhenAnyValue(x => x.Name)
				.Select(name =>
				{
					if (name == null || name.Trim().Length == 0)
					{
						return false;
					}

					return this.existingNames.All(existingName => existingName != name);
				}).ToProperty(this, x => x.CanClose, out this.canClose);
		}

		private ObservableAsPropertyHelper<bool> canClose; 
		public override bool CanClose => this.canClose.Value;

		private string name;
		public string Name
		{
			get { return this.name; }
			set { this.RaiseAndSetIfChanged(ref this.name, value); }
		}

        public string Title { get; private set; }
        public string Subtitle { get; private set; }
	}
}
