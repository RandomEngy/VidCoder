using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	using LocalResources;

	public class FileConflictDialogViewModel : ViewModelBase, IDialogViewModel
	{
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

		public bool CanClose
		{
			get
			{
				return true;
			}
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
						WindowManager.Close(this);
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
						WindowManager.Close(this);
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
						WindowManager.Close(this);
					});
				}

				return this.cancelCommand;
			}
		}

		public void OnClosing()
		{
		}
	}
}
