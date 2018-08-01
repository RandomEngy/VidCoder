using System;
using System.Windows.Input;
using ReactiveUI;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public abstract class OkCancelDialogViewModel : ReactiveObject, IClosableWindow
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		protected OkCancelDialogViewModel()
		{
			this.Cancel = ReactiveCommand.Create(() =>
			{
				this.DialogResult = false;
				this.windowManager.Close(this);
			});

			this.Accept = ReactiveCommand.Create(
				() =>
				{
					this.DialogResult = true;
					this.windowManager.Close(this);
				},
				this.WhenAnyValue(x => x.CanClose));
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

		public ICommand Cancel { get; private set; }
		public ICommand Accept { get; private set; }
	}
}
