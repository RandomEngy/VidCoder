using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using GalaSoft.MvvmLight.Command;
using VidCoder.Services;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive.Linq;

namespace VidCoder.ViewModel
{
	public class AddAutoPauseProcessDialogViewModel : OkCancelDialogViewModel
	{
		private ObservableCollection<string> currentProcesses;
		private string selectedProcess;
		private IProcesses processes;

		public AddAutoPauseProcessDialogViewModel()
		{
			this.processes = Ioc.Get<IProcesses>();
			this.currentProcesses = new ObservableCollection<string>();
			this.RefreshCurrentProcessesImpl();

			this.RefreshCurrentProcesses = ReactiveCommand.Create();
			this.RefreshCurrentProcesses.Subscribe(_ => this.RefreshCurrentProcessesImpl());
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

		public ReactiveCommand<object> RefreshCurrentProcesses { get; }
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
