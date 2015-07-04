using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class EncodeResultViewModel : ViewModelBase
	{
		private MainViewModel main = Ioc.Get<MainViewModel>();
		private ProcessingService processingService = Ioc.Get<ProcessingService>();

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

		public ProcessingService ProcessingService
		{
			get
			{
				return this.processingService;
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

							Ioc.Get<ClipboardService>().SetText(logText);
						}
						catch (IOException exception)
						{
							Ioc.Get<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception.ToString()));
						}
						catch (UnauthorizedAccessException exception)
						{
							Ioc.Get<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception.ToString()));
						}
					}));
			}
		}
	}
}
