using System;
using ReactiveUI;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public abstract class OkCancelDialogViewModel : ReactiveObject, IClosableWindow
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		protected OkCancelDialogViewModel()
		{
			this.CancelCommand = ReactiveCommand.Create();
			this.CancelCommand.Subscribe(_ =>
			{
				this.DialogResult = false;
				this.windowManager.Close(this);
			});

			this.AcceptCommand = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanClose));
			this.AcceptCommand.Subscribe(_ =>
			{
				this.DialogResult = true;
				this.windowManager.Close(this);
			});
		}

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

		public ReactiveCommand<object> CancelCommand { get; private set; }
		public ReactiveCommand<object> AcceptCommand { get; private set; }
	}
}
