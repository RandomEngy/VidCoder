using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using GalaSoft.MvvmLight.Command;
using VidCoder.Services;
using System.Windows.Input;

namespace VidCoder.ViewModel
{
	public class AddAutoPauseProcessDialogViewModel : OkCancelDialogOldViewModel
	{
		private string processName;
		private ObservableCollection<string> currentProcesses;
		private string selectedProcess;
		private IProcesses processes;

		private ICommand refreshCurrentProcessesCommand;

		public AddAutoPauseProcessDialogViewModel()
		{
			this.processes = Ioc.Get<IProcesses>();
			this.currentProcesses = new ObservableCollection<string>();
			this.RefreshCurrentProcesses();
		}

		public string ProcessName
		{
			get
			{
				return this.processName;
			}

			set
			{
				this.processName = value;
				this.RaisePropertyChanged(() => this.ProcessName);
			}
		}

		public ObservableCollection<string> CurrentProcesses
		{
			get
			{
				return this.currentProcesses;
			}
		}

		public string SelectedProcess
		{
			get
			{
				return this.selectedProcess;
			}

			set
			{
				this.selectedProcess = value;
				this.ProcessName = value;
				this.RaisePropertyChanged(() => this.SelectedProcess);
			}
		}

		public ICommand RefreshCurrentProcessesCommand
		{
			get
			{
				if (this.refreshCurrentProcessesCommand == null)
				{
					this.refreshCurrentProcessesCommand = new RelayCommand(() =>
					{
						this.RefreshCurrentProcesses();
					});
				}

				return this.refreshCurrentProcessesCommand;
			}
		}

		private void RefreshCurrentProcesses()
		{
			Process[] processes = this.processes.GetProcesses();
			this.currentProcesses.Clear();

			foreach (Process process in processes)
			{
				this.currentProcesses.Add(process.ProcessName);
			}
		}
	}
}
