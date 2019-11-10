using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class EncodeResultViewModel : ReactiveObject
	{
		private MainViewModel main = StaticResolver.Resolve<MainViewModel>();
		private ProcessingService processingService = StaticResolver.Resolve<ProcessingService>();

		private EncodeResult encodeResult;
		private EncodeJobViewModel job;

		public EncodeResultViewModel(EncodeResult result, EncodeJobViewModel job)
		{
			this.encodeResult = result;
			this.job = job;
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

		public string OutputFileSize => Utilities.FormatFileSize(this.encodeResult.SizeBytes);

		private ReactiveCommand<Unit, Unit> play;
		public ICommand Play
		{
			get
			{
				return this.play ?? (this.play = ReactiveCommand.Create(
					() =>
					{
						StaticResolver.Resolve<StatusService>().Show(MainRes.PlayingVideoStatus);
						FileService.Instance.PlayVideo(this.encodeResult.Destination);
					},
					this.WhenAnyValue(x => x.EncodeResult.Succeeded)));
			}
		}

		private ReactiveCommand<Unit, Unit> openContainingFolder;
		public ICommand OpenContainingFolder
		{
			get
			{
				return this.openContainingFolder ?? (this.openContainingFolder = ReactiveCommand.Create(
					() =>
					{
						StaticResolver.Resolve<StatusService>().Show(MainRes.OpeningFolderStatus);
						FileUtilities.OpenFolderAndSelectItem(this.encodeResult.FailedFilePath ?? this.encodeResult.Destination);
					},
					this.WhenAnyValue(x => x.EncodeResult.Succeeded, x => x.EncodeResult.FailedFilePath, (succeeded, failedFilePath) =>
					{
						return succeeded || failedFilePath != null;
					})));
			}
		}

		private ReactiveCommand<Unit, Unit> edit;
		public ICommand Edit
		{
			get
			{
				return this.edit ?? (this.edit = ReactiveCommand.Create(
					() =>
					{
						this.main.EditJob(this.job, isQueueItem: false);
					},
					this.WhenAnyValue(x => x.MainViewModel.VideoSourceState, videoSourceState =>
					{
						return videoSourceState != VideoSourceState.Scanning;
					})));
			}
		}

		private ReactiveCommand<Unit, Unit> openLog;
		public ICommand OpenLog
		{
			get
			{
				return this.openLog ?? (this.openLog = ReactiveCommand.Create(() =>
				{
					if (this.encodeResult.LogPath != null)
					{
						try
						{
							FileService.Instance.LaunchFile(this.encodeResult.LogPath);
						}
						catch (Exception exception)
						{
							StaticResolver.Resolve<IAppLogger>().LogError("Could not open log file " + this.encodeResult.LogPath + Environment.NewLine + exception);
						}
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> copyLog;
		public ICommand CopyLog
		{
			get
			{
				return this.copyLog ?? (this.copyLog = ReactiveCommand.Create(() =>
				{
					if (this.encodeResult.LogPath == null)
					{
						return;
					}

					try
					{
						string logText = File.ReadAllText(this.encodeResult.LogPath);

						StaticResolver.Resolve<ClipboardService>().SetText(logText);
					}
					catch (Exception exception)
					{
						StaticResolver.Resolve<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception));
					}
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> copyLogToPastebin;
		public ICommand CopyLogToPastebin
		{
			get
			{
				return this.copyLogToPastebin ?? (this.copyLogToPastebin = ReactiveCommand.CreateFromTask(async () =>
				{
					if (this.encodeResult.LogPath == null)
					{
						return;
					}

					try
					{
						string logText = File.ReadAllText(this.encodeResult.LogPath);
						string pastebinUrl = await StaticResolver.Resolve<PastebinService>().SubmitToPastebinAsync(logText, EnumsRes.LogOperationType_Encode + " - " + this.Job.Job.FinalOutputPath);

						StaticResolver.Resolve<ClipboardService>().SetText(pastebinUrl);
						StaticResolver.Resolve<StatusService>().Show(LogRes.PastebinSuccessStatus);
					}
					catch (Exception exception)
					{
						StaticResolver.Resolve<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception));
					}
				}));
			}
		}

		public override string ToString()
		{
			return this.Job.Job.FinalOutputPath + " " + this.StatusText;
		}
	}
}
