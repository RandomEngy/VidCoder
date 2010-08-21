using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public abstract class OkCancelDialogViewModel : ViewModelBase, IDialogViewModel
	{
		private RelayCommand cancelCommand;
		private RelayCommand acceptCommand;

		public virtual bool CanClose
		{
			get
			{
				return true;
			}
		}

		//public void RaiseCanCloseChanged()
		//{
		//    this.acceptCommand.CanExecuteChanged();
		//}

		public virtual void OnClosing()
		{
			if (this.Closing != null)
			{
				this.Closing();
			}

			WindowManager.ReportClosed(this);
		}

		public bool DialogResult { get; set; }
		public Action Closing { get; set; }

		public ICommand CancelCommand
		{
			get
			{
				if (this.cancelCommand == null)
				{
					this.cancelCommand = new RelayCommand(
						param =>
						{
							this.DialogResult = false;
							WindowManager.Close(this);
							this.OnClosing();
						});
				}

				return this.cancelCommand;
			}
		}

		public ICommand AcceptCommand
		{
			get
			{
				if (this.acceptCommand == null)
				{
					this.acceptCommand = new RelayCommand(
						param =>
						{
							this.DialogResult = true;
							WindowManager.Close(this);
							this.OnClosing();
						},
						param =>
						{
							return this.CanClose;
						});
				}

				return this.acceptCommand;
			}
		}
	}
}
