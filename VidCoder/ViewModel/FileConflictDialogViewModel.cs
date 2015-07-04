using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public class FileConflictDialogViewModel : ViewModelBase
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		private string filePath;
		private bool isFileConflict;

		private FileConflictResolution fileConflictResolution;

		private ICommand overwriteCommand;
		private ICommand renameCommand;
		private ICommand cancelCommand;

		public FileConflictDialogViewModel(string filePath, bool isFileConflict)
		{
			this.filePath = filePath;
			this.isFileConflict = isFileConflict;

			this.fileConflictResolution = FileConflictResolution.Cancel;
		}

		public string WarningText
		{
			get
			{
				string template;
				if (this.isFileConflict)
				{
					template = MiscRes.FileConflictWarning;
				}
				else
				{
					template = MiscRes.QueueFileConflictWarning;
				}

				return string.Format(template, this.filePath);
			}
		}

		public FileConflictResolution FileConflictResolution
		{
			get
			{
				return this.fileConflictResolution;
			}

			set
			{
				this.fileConflictResolution = value;
				this.RaisePropertyChanged(() => this.FileConflictResolution);
			}
		}

		public ICommand OverwriteCommand
		{
			get
			{
				if (this.overwriteCommand == null)
				{
					this.overwriteCommand = new RelayCommand(() =>
					{
						this.FileConflictResolution = FileConflictResolution.Overwrite;
						this.windowManager.Close(this);
					});
				}

				return this.overwriteCommand;
			}
		}

		public ICommand RenameCommand
		{
			get
			{
				if (this.renameCommand == null)
				{
					this.renameCommand = new RelayCommand(() =>
					{
						this.FileConflictResolution = FileConflictResolution.AutoRename;
						this.windowManager.Close(this);
					});
				}

				return this.renameCommand;
			}
		}

		public ICommand CancelCommand
		{
			get
			{
				if (this.cancelCommand == null)
				{
					this.cancelCommand = new RelayCommand(() =>
					{
						this.FileConflictResolution = FileConflictResolution.Cancel;
						this.windowManager.Close(this);
					});
				}

				return this.cancelCommand;
			}
		}
	}
}
