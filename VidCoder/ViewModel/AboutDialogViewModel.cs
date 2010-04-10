using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
    public class AboutDialogViewModel : OkCancelDialogViewModel
    {
        public string Version
        {
            get
            {
                return Utilities.CurrentVersion;
            }
        }
    }
}
