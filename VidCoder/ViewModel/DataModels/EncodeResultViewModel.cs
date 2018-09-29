using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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

		private ReactiveCommand play;
		public ReactiveCommand Play
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

		private ReactiveCommand openContainingFolder;
		public ReactiveCommand OpenContainingFolder
		{
			get
			{
				return this.openContainingFolder ?? (this.openContainingFolder = ReactiveCommand.Create(
					() =>
					{
						StaticResolver.Resolve<StatusService>().Show(MainRes.OpeningFolderStatus);
						FileUtilities.OpenFolderAndSelectItem(this.encodeResult.Destination);
					},
					this.WhenAnyValue(x => x.EncodeResult.Succeeded)));
			}
		}

		private ReactiveCommand edit;
		public ReactiveCommand Edit
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

		private ReactiveCommand openLog;
		public ReactiveCommand OpenLog
		{
			get
			{
				return this.openLog ?? (this.openLog = ReactiveCommand.Create(() =>
				{
					if (this.encodeResult.LogPath != null)
					{
						FileService.Instance.LaunchFile(this.encodeResult.LogPath);
					}
				}));
			}
		}

		private ReactiveCommand copyLog;
		public ReactiveCommand CopyLog
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
					catch (IOException exception)
					{
						StaticResolver.Resolve<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception.ToString()));
					}
					catch (UnauthorizedAccessException exception)
					{
						StaticResolver.Resolve<IMessageBoxService>().Show(this.main, string.Format(MainRes.CouldNotCopyLogError, Environment.NewLine, exception.ToString()));
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
