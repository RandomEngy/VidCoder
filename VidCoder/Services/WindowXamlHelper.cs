using System;
using Microsoft.AnyContainer;
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
		private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();

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

			this.OpenPreviewWindow = ReactiveCommand.Create(() =>
			{
				this.windowManager.OpenOrFocusWindow(typeof(PreviewWindowViewModel));
			});

			this.OpenPickerWindow = ReactiveCommand.Create(() =>
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

		public ReactiveCommand OpenPreviewWindow { get; private set; }
		public ReactiveCommand OpenPickerWindow { get; private set; }
	}
}
