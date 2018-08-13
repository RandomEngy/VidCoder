using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using VidCoder.Services;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive.Linq;
using Microsoft.AnyContainer;

namespace VidCoder.ViewModel
{
	public class AddAutoPauseProcessDialogViewModel : OkCancelDialogViewModel
	{
		private ObservableCollection<string> currentProcesses;
		private string selectedProcess;
		private IProcesses processes;

		public AddAutoPauseProcessDialogViewModel()
		{
			this.processes = StaticResolver.Resolve<IProcesses>();
			this.currentProcesses = new ObservableCollection<string>();
			this.RefreshCurrentProcessesImpl();
		}

		private string processName;
		public string ProcessName
		{
			get { return this.processName; }
			set { this.RaiseAndSetIfChanged(ref this.processName, value); }
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
				this.RaisePropertyChanged();
			}
		}

		private ReactiveCommand refreshCurrentProcesses;
		public ReactiveCommand RefreshCurrentProcesses
		{
			get
			{
				return this.refreshCurrentProcesses ?? (this.refreshCurrentProcesses = ReactiveCommand.Create(() => { this.RefreshCurrentProcessesImpl(); }));
			}
		}

		private void RefreshCurrentProcessesImpl()
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
