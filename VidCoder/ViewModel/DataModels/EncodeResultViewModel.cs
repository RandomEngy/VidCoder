using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class EncodeResultViewModel : ReactiveObject
	{
		private MainViewModel main = Ioc.Get<MainViewModel>();
		private ProcessingService processingService = Ioc.Get<ProcessingService>();

		private EncodeResult encodeResult;
		private EncodeJobViewModel job;

		public EncodeResultViewModel(EncodeResult result, EncodeJobViewModel job)
		{
			this.encodeResult = result;
			this.job = job;

			this.Play = ReactiveCommand.Create();
			this.Play.Subscribe(_ => this.PlayImpl());

			this.OpenContainingFolder = ReactiveCommand.Create();
			this.OpenContainingFolder.Subscribe(_ => this.OpenContainingFolderImpl());

			this.Edit = ReactiveCommand.Create(this.WhenAnyValue(x => x.MainViewModel.VideoSourceState, videoSourceState =>
			{
				return videoSourceState != VideoSourceState.Scanning;
			}));
			this.Edit.Subscribe(_ => this.EditImpl());

			this.OpenLog = ReactiveCommand.Create();
			this.OpenLog.Subscribe(_ => this.OpenLogImpl());

			this.CopyLog = ReactiveCommand.Create();
			this.CopyLog.Subscribe(_ => this.CopyLogImpl());
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

		public ReactiveCommand<object> Play { get; }
		private void PlayImpl()
		{
			Ioc.Get<StatusService>().Show(MainRes.PlayingVideoStatus);
			FileService.Instance.PlayVideo(this.encodeResult.Destination);
		}

		public ReactiveCommand<object> OpenContainingFolder { get; }
		private void OpenContainingFolderImpl()
		{
			Ioc.Get<StatusService>().Show(MainRes.OpeningFolderStatus);
			Process.Start("explorer.exe", "/select," + this.encodeResult.Destination);
		}

		public ReactiveCommand<object> Edit { get; }
		private void EditImpl()
		{
			this.main.EditJob(this.job, isQueueItem: false);
		}

		public ReactiveCommand<object> OpenLog { get; }
		private void OpenLogImpl()
		{
			FileService.Instance.LaunchFile(this.encodeResult.LogPath);
		}

		public ReactiveCommand<object> CopyLog { get; }
		private void CopyLogImpl()
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
		}
	}
}
