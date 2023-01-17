using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Model;

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

		public MainViewModel MainViewModel => this.main;

		public EncodeJobViewModel Job => this.job;

		public ProcessingService ProcessingService => this.processingService;

		public EncodeResult EncodeResult => this.encodeResult;

		public bool SourceFileExists { get; set; } = true;

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

		public string TimeDisplay => this.encodeResult.EncodeTime.FormatShort();

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
						return videoSourceState != VideoSourceState.Scanning && this.SourceFileExists;
					})));
			}
		}

		public bool ShowCompare
		{
			get
			{
				VCJob vcJob = this.Job.Job;
				return this.encodeResult.Succeeded
					&& vcJob.SourceType == SourceType.File
					&& (vcJob.RangeType == VideoRangeType.All
						|| vcJob.RangeType == VideoRangeType.Chapters && vcJob.ChapterStart == 1
						|| vcJob.RangeType == VideoRangeType.Seconds && vcJob.SecondsStart == 0
						|| vcJob.RangeType == VideoRangeType.Frames && vcJob.FramesStart == 0);
			}
		}

		private ReactiveCommand<Unit, Unit> compare;
		public ICommand Compare
		{
			get
			{
				return this.compare ?? (this.compare = ReactiveCommand.Create(
					() =>
					{
						long inputFileSizeBytes = 0;
						try
						{
							FileInfo inputFileInfo = new FileInfo(this.Job.Job.SourcePath);
							if (!inputFileInfo.Exists)
							{
								StaticResolver.Resolve<IMessageBoxService>().Show(this.main, CommonRes.FileNoLongerExists);
								return;
							}

							inputFileSizeBytes = inputFileInfo.Length;
						}
						catch
						{
							// It's OK if we don't get the file size.
						}

						OutputSizeInfo outputSize = JsonEncodeFactory.GetOutputSize(this.Job.Profile, this.Job.SourceTitle);
						CompareWindowViewModel compareViewModel = StaticResolver.Resolve<IWindowManager>().OpenOrFocusWindow<CompareWindowViewModel>(this.main);
						compareViewModel.OpenVideos(this.Job.Job.SourcePath, this.Job.Job.FinalOutputPath, inputFileSizeBytes, this.encodeResult.SizeBytes);
					},
					MvvmUtilities.CreateConstantObservable(this.SourceFileExists)));
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
					this.CopyLogImpl();
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> copyLogAndReportProblem;
		public ICommand CopyLogAndReportProblem
		{
			get
			{
				return this.copyLogAndReportProblem ?? (this.copyLogAndReportProblem = ReactiveCommand.Create(() =>
				{
					this.CopyLogImpl();
					FileService.Instance.ReportBug();
				}));
			}
		}

		private void CopyLogImpl()
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
		}

		public override string ToString()
		{
			return this.Job.Job.FinalOutputPath + " " + this.StatusText;
		}
	}
}
