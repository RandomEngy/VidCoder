using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	/// <summary>
	/// Controls opening/closing of windows.
	/// </summary>
	public class WindowManagerService : ObservableObject
	{
		private MainViewModel main = Ioc.Get<MainViewModel>();
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

	    private RelayCommand openOptionsCommand;
	    public RelayCommand OpenOptionsCommand
	    {
	        get
	        {
	            return this.openOptionsCommand ?? (this.openOptionsCommand = new RelayCommand(() =>
	            {
                    var optionsVM = new OptionsDialogViewModel(Ioc.Get<IUpdater>());
					this.windowManager.OpenDialog(optionsVM);

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
	}
}
