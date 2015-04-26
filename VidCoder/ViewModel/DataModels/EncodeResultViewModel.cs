using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Model;
using System.Globalization;
using System.Windows.Input;
using System.IO;
using VidCoder.Services;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	using System.Diagnostics;
	using Resources;
	using Properties;

	public class EncodeResultViewModel : ViewModelBase
	{
		private MainViewModel main = Ioc.Container.GetInstance<MainViewModel>();
		private ProcessingViewModel processingVM = Ioc.Container.GetInstance<ProcessingViewModel>();

		private EncodeResult encodeResult;
		private EncodeJobViewModel job;

		public EncodeResultViewModel(EncodeResult result, EncodeJobViewModel job)
		{
			this.encodeResult = result;
			this.job = job;

			Messenger.Default.Register<ScanningChangedMessage>(
				this,
				message =>
					{
						this.EditCommand.RaiseCanExecuteChanged();
					});
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.main;
			}
		}

		public EncodeJobViewModel Job
		{
			get
			{
				return this.job;
			}
		}

		public ProcessingViewModel ProcessingVM
		{
			get
			{
				return this.processingVM;
			}
		}

		public EncodeResult EncodeResult
		{
			get
			{
				return this.encodeResult;
			}
		}

		public string StatusText
		{
			get
			{
				switch (this.encodeResult.Status)
				{
					case EncodeResultStatus.Succeeded:
						return CommonRes.Succeeded;
					case EncodeResultStatus.Failed:
						return CommonRes.Failed;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		public string TimeDisplay
		{
			get
			{
				return this.encodeResult.EncodeTime.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
			}
		}

		public string StatusImage
		{
			get
			{
				switch (this.encodeResult.Status)
				{
					case EncodeResultStatus.Succeeded:
						return "/Icons/succeeded.png";
					case EncodeResultStatus.Failed:
						return "/Icons/failed.png";
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		private RelayCommand playCommand;
		public RelayCommand PlayCommand
		{
			get
			{
				return this.playCommand ?? (this.playCommand = new RelayCommand(() =>
					{
						Messenger.Default.Send(new StatusMessage { Message = MainRes.PlayingVideoStatus });
						FileService.Instance.PlayVideo(this.encodeResult.Destination);
					}));
			}
		}

		private RelayCommand openContainingFolderCommand;
		public RelayCommand OpenContainingFolderCommand
		{
			get
			{
				return this.openContainingFolderCommand ?? (this.openContainingFolderCommand = new RelayCommand(() =>
					{
						Messenger.Default.Send(new StatusMessage { Message = MainRes.OpeningFolderStatus });
						Process.Start("explorer.exe", "/select," + this.encodeResult.Destination);
					}));
			}
		}

		private RelayCommand editCommand;
		public RelayCommand EditCommand
		{
			get
			{
				return this.editCommand ?? (this.editCommand = new RelayCommand(() =>
					{
						this.main.EditJob(this.job, isQueueItem: false);
					}, () =>
					{
						return !this.main.ScanningSource;
					}));
			}
		}

		private RelayCommand openLogCommand;
		public RelayCommand OpenLogCommand
		{
			get
			{
				return this.openLogCommand ?? (this.openLogCommand = new RelayCommand(() =>
					{
						FileService.Instance.LaunchFile(this.encodeResult.LogPath);
					}));
			}
		}

		private RelayCommand copyLogCommand;
		public RelayCommand CopyLogCommand
		{
			get
			{
				return this.copyLogCommand ?? (this.copyLogCommand = new RelayCommand(() =>
					{
						try
						{
							string logText = File.ReadAllText(this.encodeResult.LogPath);

							Ioc.Container.GetInstance<ClipboardService>().SetText(logText);
						}
						catch (IOException exception)
						{
							Ioc.Container.GetInstance<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception.ToString()));
						}
						catch (UnauthorizedAccessException exception)
						{
							Ioc.Container.GetInstance<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception.ToString()));
						}
					}));
			}
		}
	}
}
