using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop.SourceData;
using VidCoder.Messages;

namespace VidCoder.ViewModel
{
	public class ChapterViewModel : ViewModelBase
	{
		public ChapterViewModel(Chapter chapter)
		{
			this.Chapter = chapter;
		}

		public Chapter Chapter { get; private set; }

		public int ChapterNumber
		{
			get
			{
				return this.Chapter.ChapterNumber;
			}
		}

		private bool isHighlighted;
		public bool IsHighlighted
		{
			get
			{
				return this.isHighlighted;
			}

			set
			{
				this.isHighlighted = value;
				this.RaisePropertyChanged(() => this.IsHighlighted);
				Messenger.Default.Send(new HighlightedChapterChangedMessage());
			}
		}
	}
}
