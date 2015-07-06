using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace VidCoder.Services.Windows
{
	public class WindowMenuItemViewModel : ReactiveObject
	{
		public WindowMenuItemViewModel(WindowDefinition definition)
		{
			var windowManager = Ioc.Get<IWindowManager>();
			this.Definition = definition;

			if (definition.CanOpen == null)
			{
				Observable.Never<bool>().StartWith(true).ToProperty(this, x => x.CanOpen, out this.canOpen);
			}
			else
			{
				definition.CanOpen().ToProperty(this, x => x.CanOpen, out this.canOpen);
			}

			windowManager.WindowOpened += (o, e) =>
			{
				if (e.Value == definition.ViewModelType)
				{
					this.IsOpen = true;
				}
			};
			windowManager.WindowClosed += (o, e) =>
			{
				if (e.Value == definition.ViewModelType)
				{
					this.IsOpen = false;
				}
			};

			this.Command = windowManager.CreateOpenCommand(definition.ViewModelType);
		}

		public WindowDefinition Definition { get; private set; }

		public ICommand Command { get; private set; }

		private bool isOpen;
		public bool IsOpen
		{
			get { return this.isOpen; }
			set { this.RaiseAndSetIfChanged(ref this.isOpen, value); }
		}

		private ObservableAsPropertyHelper<bool> canOpen;
		public bool CanOpen
		{
			get { return this.canOpen.Value; }
		}
	}
}
