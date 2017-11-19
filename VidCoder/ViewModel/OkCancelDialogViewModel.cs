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
			this.Cancel = ReactiveCommand.Create();
			this.Cancel.Subscribe(_ =>
			{
				this.DialogResult = false;
				this.windowManager.Close(this);
			});

			this.Accept = ReactiveCommand.Create(this.WhenAnyValue(x => x.CanClose));
			this.Accept.Subscribe(_ =>
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

		public virtual bool OnClosing()
		{
			return true;
		}

		public bool DialogResult { get; set; }

		public ReactiveCommand<object> Cancel { get; private set; }
		public ReactiveCommand<object> Accept { get; private set; }
	}
}
