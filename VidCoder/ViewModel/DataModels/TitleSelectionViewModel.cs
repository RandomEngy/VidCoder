using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.SourceData;

namespace VidCoder.ViewModel
{
	public class TitleSelectionViewModel : ViewModelBase
	{
		private QueueTitlesDialogViewModel titlesDialogVM;
		private bool selected;

		public TitleSelectionViewModel(Title title, QueueTitlesDialogViewModel titlesDialogVM)
		{
			this.Title = title;
			this.titlesDialogVM = titlesDialogVM;
		}

		public bool Selected
		{
			get
			{
				return this.selected;
			}

			set
			{
				this.selected = value;
				this.NotifyPropertyChanged("Selected");
				this.titlesDialogVM.HandleCheckChanged(this, value);
			}
		}

		public Title Title { get; set; }

		public string Text
		{
			get
			{
				return this.Title.Display;
			}
		}

		/// <summary>
		/// Set the selected value for this item.
		/// </summary>
		/// <param name="newValue"></param>
		public void SetSelected(bool newValue)
		{
			this.selected = newValue;
			this.NotifyPropertyChanged("Selected");
		}
	}
}
