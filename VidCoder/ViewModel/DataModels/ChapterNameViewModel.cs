using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;

namespace VidCoder.ViewModel
{
	public class ChapterNameViewModel : ViewModelBase
	{
		private int number;
		private string title;

		public int Number
		{
			get
			{
				return this.number;
			}

			set
			{
				this.number = value;
				this.RaisePropertyChanged(() => this.Number);
			}
		}

		public string Title
		{
			get
			{
				return this.title;
			}

			set
			{
				this.title = value;
				this.RaisePropertyChanged(() => this.Title);
			}
		}

		private string startTime;
		public string StartTime
		{
			get
			{
				return this.startTime;
			}

			set
			{
				this.startTime = value;
				this.RaisePropertyChanged(() => this.StartTime);
			}
		}
	}
}
