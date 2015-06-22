namespace VidCoder.ViewModel
{
	using System;
	using System.Globalization;
	using System.Windows.Threading;
	using GalaSoft.MvvmLight;
	using GalaSoft.MvvmLight.Command;
	using Model;
	using Resources;
	using Services;

	public class ShutdownWarningViewModel : OkCancelDialogOldViewModel
	{
		private EncodeCompleteActionType actionType;

		private ISystemOperations systemOperations = Ioc.Container.GetInstance<ISystemOperations>();
		private int secondsRemaining = 30;
		private DispatcherTimer timer;

		public ShutdownWarningViewModel(EncodeCompleteActionType actionType)
		{
			this.actionType = actionType;

			this.timer = new DispatcherTimer();
			this.timer.Interval = TimeSpan.FromSeconds(1);
			this.timer.Tick += (o, e) =>
			{
				secondsRemaining--;
				this.RaisePropertyChanged(() => this.Message);

				if (secondsRemaining == 0)
				{
					this.timer.Stop();
					this.CancelCommand.Execute(null);
					this.ExecuteAction();
				}
			};

			this.timer.Start();
		}

		public string Title
		{
			get
			{
				switch (actionType)
				{
					case EncodeCompleteActionType.Sleep:
						return MiscRes.EncodeCompleteWarning_SleepTitle;
					case EncodeCompleteActionType.LogOff:
						return MiscRes.EncodeCompleteWarning_LogOffTitle;
					case EncodeCompleteActionType.Shutdown:
						return MiscRes.EncodeCompleteWarning_ShutdownTitle;
					case EncodeCompleteActionType.Hibernate:
						return MiscRes.EncodeCompleteWarning_HibernateTitle;
					default:
						return string.Empty;
				}
			}
		}

		public string Message
		{
			get
			{
				string messageFormat = string.Empty;
				switch (actionType)
				{
					case EncodeCompleteActionType.Sleep:
						messageFormat = MiscRes.EncodeCompleteWarning_SleepMessage;
						break;
					case EncodeCompleteActionType.LogOff:
						messageFormat = MiscRes.EncodeCompleteWarning_LogOffMessage;
						break;
					case EncodeCompleteActionType.Shutdown:
						messageFormat = MiscRes.EncodeCompleteWarning_ShutdownMessage;
						break;
					case EncodeCompleteActionType.Hibernate:
						messageFormat = MiscRes.EncodeCompleteWarning_HibernateMessage;
						break;
				}

				return string.Format(CultureInfo.CurrentCulture, messageFormat, this.secondsRemaining);
			}
		}

		private void ExecuteAction()
		{
			switch (actionType)
			{
				case EncodeCompleteActionType.Sleep:
					this.systemOperations.Sleep();
					break;
				case EncodeCompleteActionType.LogOff:
					this.systemOperations.LogOff();
					break;
				case EncodeCompleteActionType.Shutdown:
					this.systemOperations.ShutDown();
					break;
				case EncodeCompleteActionType.Hibernate:
					this.systemOperations.Hibernate();
					break;
			}
		}

		private RelayCommand cancelOperationCommand;
		public RelayCommand CancelOperationCommand
		{
			get
			{
				return this.cancelOperationCommand ?? (this.cancelOperationCommand = new RelayCommand(() =>
				{
					this.timer.Stop();
					this.CancelCommand.Execute(null);
				}));
			}
		}
	}
}
