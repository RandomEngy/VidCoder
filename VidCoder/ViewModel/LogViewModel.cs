using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Microsoft.Practices.Unity;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class LogViewModel : OkCancelDialogViewModel
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private ILogger logger = Unity.Container.Resolve<ILogger>();

		private ICommand clearLogCommand;

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
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
	}
}
