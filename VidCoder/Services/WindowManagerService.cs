using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	/// <summary>
	/// Controls opening/closing of windows.
	/// </summary>
	public class WindowManagerService : ObservableObject
	{
		private MainViewModel main = Ioc.Container.GetInstance<MainViewModel>();

		private bool encodingWindowOpen;
		private bool previewWindowOpen;
		private bool logWindowOpen;
		private bool encodeDetailsWindowOpen;
	    private bool pickerWindowOpen;

		public bool EncodingWindowOpen
		{
			get
			{
				return this.encodingWindowOpen;
			}

			set
			{
				this.encodingWindowOpen = value;
				this.RaisePropertyChanged(() => this.EncodingWindowOpen);
			}
		}

		public bool PreviewWindowOpen
		{
			get
			{
				return this.previewWindowOpen;
			}

			set
			{
				this.previewWindowOpen = value;
				this.RaisePropertyChanged(() => this.PreviewWindowOpen);
			}
		}

		public bool LogWindowOpen
		{
			get
			{
				return this.logWindowOpen;
			}

			set
			{
				this.logWindowOpen = value;
				this.RaisePropertyChanged(() => this.LogWindowOpen);
			}
		}

		public bool EncodeDetailsWindowOpen
		{
			get
			{
				return this.encodeDetailsWindowOpen;
			}

			set
			{
				this.encodeDetailsWindowOpen = value;
				this.RaisePropertyChanged(() => this.EncodeDetailsWindowOpen);
			}
		}

	    public bool PickerWindowOpen
	    {
	        get { return this.pickerWindowOpen; }
	        set
	        {
	            this.pickerWindowOpen = value;
	            this.RaisePropertyChanged(() => this.PickerWindowOpen);
	        }
	    }

		private ICommand openEncodingWindowCommand;
		public ICommand OpenEncodingWindowCommand
		{
			get
			{
				if (this.openEncodingWindowCommand == null)
				{
					this.openEncodingWindowCommand = new RelayCommand(() =>
					{
						this.OpenEncodingWindow();
					});
				}

				return this.openEncodingWindowCommand;
			}
		}

		private ICommand openPreviewWindowCommand;
		public ICommand OpenPreviewWindowCommand
		{
			get
			{
				if (this.openPreviewWindowCommand == null)
				{
					this.openPreviewWindowCommand = new RelayCommand(() =>
					{
						this.OpenPreviewWindow();
					});
				}

				return this.openPreviewWindowCommand;
			}
		}

	    private RelayCommand openPickerWindowCommand;
	    public RelayCommand OpenPickerWindowCommand
	    {
	        get
	        {
	            return this.openPickerWindowCommand ?? (this.openPickerWindowCommand = new RelayCommand(() =>
	            {
					this.OpenPickerWindow();
	            }));
	        }
	    }

		private ICommand openLogWindowCommand;
		public ICommand OpenLogWindowCommand
		{
			get
			{
				if (this.openLogWindowCommand == null)
				{
					this.openLogWindowCommand = new RelayCommand(() =>
					{
						this.OpenLogWindow();
					});
				}

				return this.openLogWindowCommand;
			}
		}

		private RelayCommand openEncodeDetailsWindowCommand;
		public RelayCommand OpenEncodeDetailsWindowCommand
		{
			get
			{
				return this.openEncodeDetailsWindowCommand ?? (this.openEncodeDetailsWindowCommand = new RelayCommand(() =>
					{
						this.OpenEncodeDetailsWindow();
					},
					() =>
					{
						return this.main.ProcessingService.Encoding;
					}));
			}
		}

	    private RelayCommand openOptionsCommand;
	    public RelayCommand OpenOptionsCommand
	    {
	        get
	        {
	            return this.openOptionsCommand ?? (this.openOptionsCommand = new RelayCommand(() =>
	            {
                    var optionsVM = new OptionsDialogViewModel(Ioc.Container.GetInstance<IUpdater>());
                    WindowManager.OpenDialog(optionsVM, this.main);
                    if (optionsVM.DialogResult)
                    {
                        Messenger.Default.Send(new OutputFolderChangedMessage());
                    }
	            }));
	        }
	    }

        private RelayCommand openUpdatesCommand;
        public RelayCommand OpenUpdatesCommand
        {
            get
            {
                return this.openUpdatesCommand ?? (this.openUpdatesCommand = new RelayCommand(() =>
                {
                    Config.OptionsDialogLastTab = 5;
                    this.OpenOptionsCommand.Execute(null);
                }));
            }
        }

		public void OpenEncodingWindow()
		{
			var encodingWindow = WindowManager.FindWindow<EncodingViewModel>();
			this.EncodingWindowOpen = true;

			if (encodingWindow == null)
			{
				encodingWindow = new EncodingViewModel(Ioc.Container.GetInstance<PresetsService>().SelectedPreset.Preset);
				encodingWindow.Closing = () =>
				{
					this.EncodingWindowOpen = false;

					// Focus the main window after closing. If they used a keyboard shortcut to close it might otherwise give up
					// focus on the app altogether.
					WindowManager.FocusWindow(this.main);
				};
				WindowManager.OpenWindow(encodingWindow, this.main);
			}
			else
			{
				WindowManager.FocusWindow(encodingWindow);
			}
		}

		public void OpenPreviewWindow()
		{
			var previewWindow = WindowManager.FindWindow<PreviewViewModel>();
			this.PreviewWindowOpen = true;

			if (previewWindow == null)
			{
				previewWindow = new PreviewViewModel();
				previewWindow.Closing = () =>
				{
					this.PreviewWindowOpen = false;
				};
				WindowManager.OpenWindow(previewWindow, this.main);
			}
			else
			{
				WindowManager.FocusWindow(previewWindow);
			}
		}

		public void OpenLogWindow()
		{
			var logWindow = WindowManager.FindWindow<LogViewModel>();
			this.LogWindowOpen = true;

			if (logWindow == null)
			{
				logWindow = new LogViewModel();
				logWindow.Closing = () =>
				{
					this.LogWindowOpen = false;
				};
				WindowManager.OpenWindow(logWindow, this.main);
			}
			else
			{
				WindowManager.FocusWindow(logWindow);
			}
		}

		public void OpenEncodeDetailsWindow()
		{
			var encodeDetailsWindow = WindowManager.FindWindow<EncodeDetailsViewModel>();
			this.EncodeDetailsWindowOpen = true;

			if (encodeDetailsWindow == null)
			{
				encodeDetailsWindow = new EncodeDetailsViewModel();
				encodeDetailsWindow.Closing = () =>
				{
					this.EncodeDetailsWindowOpen = false;
				};

				Config.EncodeDetailsWindowOpen = true;

				WindowManager.OpenWindow(encodeDetailsWindow, this.main);
			}
			else
			{
				WindowManager.FocusWindow(encodeDetailsWindow);
			}
		}

		public void CloseEncodeDetailsWindow()
		{
			var encodeDetailsWindow = WindowManager.FindWindow<EncodeDetailsViewModel>();
			if (encodeDetailsWindow != null)
			{
				WindowManager.Close(encodeDetailsWindow);
			}
		}

		public void OpenPickerWindow()
		{
			var pickerWindow = WindowManager.FindWindow<PickerWindowViewModel>();
			this.PickerWindowOpen = true;

			if (pickerWindow == null)
			{
				pickerWindow = new PickerWindowViewModel(Ioc.Container.GetInstance<PickersService>().SelectedPicker.Picker);
				pickerWindow.Closing = () =>
				{
					this.PickerWindowOpen = false;
				};

				Config.PickerWindowOpen = true;

				WindowManager.OpenWindow(pickerWindow, this.main);
			}
			else
			{
				WindowManager.FocusWindow(pickerWindow);
			}
		}

		public void OpenQueueTitlesWindow()
		{
			var queueTitlesWindow = WindowManager.FindWindow<QueueTitlesWindowViewModel>();
			if (queueTitlesWindow == null)
			{
				queueTitlesWindow = new QueueTitlesWindowViewModel();
				WindowManager.OpenWindow(queueTitlesWindow, this.main);
			}
			else
			{
				WindowManager.FocusWindow(queueTitlesWindow);
			}
		}

		public void RefreshEncoding()
		{
			this.OpenEncodeDetailsWindowCommand.RaiseCanExecuteChanged();
		}
	}
}
