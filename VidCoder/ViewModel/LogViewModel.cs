using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using System.Windows.Input;

namespace VidCoder.ViewModel
{
    public class LogViewModel : OkCancelDialogViewModel
    {
        private Logger logger;

        private ICommand clearLogCommand;

        public LogViewModel(Logger logger)
        {
            this.logger = logger;
        }

        public string LogText
        {
            get
            {
                return logger.LogText;
            }
        }

        public ICommand ClearLogCommand
        {
            get
            {
                if (this.clearLogCommand == null)
                {
                    this.clearLogCommand = new RelayCommand(param =>
                    {
                        this.logger.ClearLog();
                        this.NotifyPropertyChanged("LogText");
                    });
                }

                return this.clearLogCommand;
            }
        }

        public void RefreshLogs()
        {
            this.NotifyPropertyChanged("LogText");
        }
    }
}
