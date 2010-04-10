using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                this.NotifyPropertyChanged("Number");
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
                this.NotifyPropertyChanged("Title");
            }
        }
    }
}
