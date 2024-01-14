using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Windows.Input;
using Microsoft.AnyContainer;
using ReactiveUI;

namespace VidCoder.Services.Windows;

public class WindowMenuItemViewModel : ReactiveObject
{
	private bool automaticChange = false;

	public WindowMenuItemViewModel(WindowDefinition definition)
	{
		var windowManager = StaticResolver.Resolve<IWindowManager>();
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
				this.automaticChange = true;
				this.IsOpen = true;
				this.automaticChange = false;
			}
		};
		windowManager.WindowClosed += (o, e) =>
		{
			if (e.Value == definition.ViewModelType)
			{
				this.automaticChange = true;
				this.IsOpen = false;
				this.automaticChange = false;
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

		set
		{
			// We want to ignore attempts to toggle from the Windows menu
			if (this.automaticChange)
			{
				this.RaiseAndSetIfChanged(ref this.isOpen, value);
			}
		}
	}

	private ObservableAsPropertyHelper<bool> canOpen;
	public bool CanOpen => this.canOpen.Value;
}
