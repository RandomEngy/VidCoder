﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.EventArgs;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.Services
{
	public class LogCoordinator : ReactiveObject
	{
		private readonly IAppLogger allAppLogger = StaticResolver.Resolve<AllAppLogger>();

		public LogCoordinator()
		{
			var generalAppLogger = new LogViewModel(StaticResolver.Resolve<IAppLogger>())
			{
				OperationType = LogOperationType.General
			};

			this.Logs.Add(generalAppLogger);

			this.Logs.Connect().Bind(this.LogsBindable).Subscribe();

			this.selectedLog = generalAppLogger;

			if (!CustomConfig.UseWorkerProcess)
			{
				// The central logger always listens for messages when encoding locally
				HandBrakeUtils.MessageLogged += this.OnMessageLoggedLocal;
				HandBrakeUtils.ErrorLogged += this.OnErrorLoggedLocal;
			}
		}

		public SourceList<LogViewModel> Logs { get; } = new SourceList<LogViewModel>();
		public IObservableCollection<LogViewModel> LogsBindable { get; } = new ObservableCollectionExtended<LogViewModel>();

		private LogViewModel selectedLog;
		public LogViewModel SelectedLog
		{
			get => this.selectedLog;
			set => this.RaiseAndSetIfChanged(ref this.selectedLog, value);
		}

		private ReactiveCommand<Unit, Unit> openLogFolder;
		public ICommand OpenLogFolder
		{
			get
			{
				return this.openLogFolder ?? (this.openLogFolder = ReactiveCommand.Create(
					       () =>
					       {
						       string logFolder = Utilities.LogsFolder;

						       if (Directory.Exists(logFolder))
						       {
							       FileService.Instance.LaunchFile(logFolder);
						       }
					       },
					       MvvmUtilities.CreateConstantObservable(Directory.Exists(Utilities.LogsFolder))));
			}
		}

		public void AddLogger(IAppLogger logger, LogOperationType logOperationType, string operationPath)
		{
			this.Logs.Add(
				new LogViewModel(logger) { OperationPath = operationPath, OperationType = logOperationType });
		}

		private void OnMessageLoggedLocal(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Message,
				Source = LogSource.HandBrake,
				Text = e.Message
			};

			this.allAppLogger.AddEntry(entry);
		}

		private void OnErrorLoggedLocal(object sender, MessageLoggedEventArgs e)
		{
			var entry = new LogEntry
			{
				LogType = LogType.Error,
				Source = LogSource.HandBrake,
				Text = "ERROR: " + e.Message
			};

			this.allAppLogger.AddEntry(entry);
		}
	}
}
