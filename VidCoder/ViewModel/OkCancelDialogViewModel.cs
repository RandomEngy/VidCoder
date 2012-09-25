using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public abstract class OkCancelDialogViewModel : ViewModelBase, IDialogViewModel
	{
		public virtual bool CanClose
		{
			get
			{
				return true;
			}
		}

		public virtual void OnClosing()
		{
			if (this.Closing != null)
			{
				this.Closing();
			}

			this.Closed = true;
			WindowManager.ReportClosed(this);
		}

		public bool Closed { get; set; }

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
						WindowManager.Close(this);
						this.OnClosing();
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
						WindowManager.Close(this);
						this.OnClosing();
					}, () =>
					{
						return this.CanClose;
					}));
			}
		}
	}
}
