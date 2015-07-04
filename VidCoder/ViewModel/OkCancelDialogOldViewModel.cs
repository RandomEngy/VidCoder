using System;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public abstract class OkCancelDialogOldViewModel : ViewModelBase, IDialogViewModel
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		public virtual bool CanClose
		{
			get
			{
				return true;
			}
		}

		public virtual void OnClosing()
		{
		}

		public bool DialogResult { get; set; }
		public Action Closing { get; set; }

		private RelayCommand cancelCommand;
		public RelayCommand CancelCommand
		{
			get
			{
				return this.cancelCommand ?? (this.cancelCommand = new RelayCommand(() =>
				{
					this.DialogResult = false;
					this.windowManager.Close(this);
				}));
			}
		}

		private RelayCommand acceptCommand;
		public RelayCommand AcceptCommand
		{
			get
			{
				return this.acceptCommand ?? (this.acceptCommand = new RelayCommand(() =>
				{
					this.DialogResult = true;
					this.windowManager.Close(this);
				}, () =>
				{
					return this.CanClose;
				}));
			}
		}
	}
}
