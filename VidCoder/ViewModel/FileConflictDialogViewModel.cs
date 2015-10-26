using System;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public class FileConflictDialogViewModel : ReactiveObject
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		private string filePath;
		private bool isFileConflict;

		public FileConflictDialogViewModel(string filePath, bool isFileConflict)
		{
			this.filePath = filePath;
			this.isFileConflict = isFileConflict;

			this.fileConflictResolution = FileConflictResolution.Cancel;

			this.Overwrite = ReactiveCommand.Create();
			this.Overwrite.Subscribe(_ => this.OverwriteImpl());

			this.Rename = ReactiveCommand.Create();
			this.Rename.Subscribe(_ => this.RenameImpl());

			this.Cancel = ReactiveCommand.Create();
			this.Cancel.Subscribe(_ => this.CancelImpl());
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

		private FileConflictResolution fileConflictResolution;
		public FileConflictResolution FileConflictResolution
		{
			get { return this.fileConflictResolution; }
			set { this.RaiseAndSetIfChanged(ref this.fileConflictResolution, value); }
		}

		public ReactiveCommand<object> Overwrite { get; }
		private void OverwriteImpl()
		{
			this.FileConflictResolution = FileConflictResolution.Overwrite;
			this.windowManager.Close(this);
		}

		public ReactiveCommand<object> Rename { get; }
		private void RenameImpl()
		{
			this.FileConflictResolution = FileConflictResolution.AutoRename;
			this.windowManager.Close(this);
		}

		public ReactiveCommand<object> Cancel { get; }
		private void CancelImpl()
		{
			this.FileConflictResolution = FileConflictResolution.Cancel;
			this.windowManager.Close(this);
		}
	}
}
