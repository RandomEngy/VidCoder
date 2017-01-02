using System;
using ReactiveUI;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;

namespace VidCoder
{
	/// <summary>
	/// Helps open and track windows in XAML files.
	/// </summary>
	public class WindowXamlHelper : ReactiveObject
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		public WindowXamlHelper()
		{
			this.windowManager.WindowOpened += (o, e) =>
			{
				if (e.Value == typeof(PreviewWindowViewModel))
				{
					this.PreviewWindowOpen = true;
				}
			};
			this.windowManager.WindowClosed += (o, e) =>
			{
				if (e.Value == typeof(PreviewWindowViewModel))
				{
					this.PreviewWindowOpen = false;
				}
			};

			this.OpenPreviewWindow = ReactiveCommand.Create();
			this.OpenPreviewWindow.Subscribe(_ =>
			{
				this.windowManager.OpenOrFocusWindow(typeof(PreviewWindowViewModel));
			});

			this.OpenPickerWindow = ReactiveCommand.Create();
			this.OpenPickerWindow.Subscribe(_ =>
			{
				this.windowManager.OpenOrFocusWindow(typeof(PickerWindowViewModel));
			});
		}

		private bool previewWindowOpen;
		public bool PreviewWindowOpen
		{
			get { return this.previewWindowOpen; }
			set { this.RaiseAndSetIfChanged(ref this.previewWindowOpen, value); }
		}

		public ReactiveCommand<object> OpenPreviewWindow { get; private set; }
		public ReactiveCommand<object> OpenPickerWindow { get; private set; }
	}
}
