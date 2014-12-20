using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using VidCoder.Resources;

namespace VidCoder.ViewModel
{
	public class ChooseNameViewModel : OkCancelDialogViewModel
	{
        private IEnumerable<string> existingNames;

		public ChooseNameViewModel(string word, IEnumerable<string> existingNames)
		{
            this.existingNames = existingNames;

		    this.Title = string.Format(MiscRes.NameDialogTitle, word);
		    this.Subtitle = string.Format(MiscRes.ChooseName, word);
		}

		public override bool CanClose
		{
			get
			{
				if (this.Name == null || this.Name.Trim().Length == 0)
				{
					return false;
				}

			    return this.existingNames.All(existingName => existingName != this.Name);
			}
		}

		private string name;
		public string Name
		{
			get
			{
				return this.name;
			}

			set
			{
				this.name = value;
				this.RaisePropertyChanged(() => this.Name);
				this.AcceptCommand.RaiseCanExecuteChanged();
			}
		}

        public string Title { get; private set; }
        public string Subtitle { get; private set; }
	}
}
