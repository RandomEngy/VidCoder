using System;
using System.Diagnostics;
using System.Reactive.Linq;
using ReactiveUI;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class EncodeDetailsWindowViewModel : ReactiveObject
	{
		private IObservable<EncodeProgress> progressObservable; 

		public EncodeDetailsWindowViewModel()
		{
			ProcessingService processingService = Ioc.Get<ProcessingService>();
			this.progressObservable = processingService.WhenAnyValue(x => x.EncodeProgress);

			this.GetPropertyWatcher(encodeProgress => encodeProgress.OverallProgressFraction * 100)
				.ToProperty(this, x => x.OverallProgressPercent, out this.overallProgressPercent);

			this.GetPropertyWatcher(encodeProgress => encodeProgress.TaskNumber + "/" + encodeProgress.TotalTasks)
				.ToProperty(this, x => x.TaskNumberDisplay, out this.taskNumberDisplay);

			this.GetPropertyWatcher(encodeProgress => Utilities.FormatTimeSpan(encodeProgress.OverallElapsedTime))
				.ToProperty(this, x => x.OverallElapsedTime, out this.overallElapsedTime);

			this.GetPropertyWatcher(encodeProgress => Utilities.FormatTimeSpan(encodeProgress.OverallEta))
				.ToProperty(this, x => x.OverallEta, out this.overallEta);

			this.GetPropertyWatcher(encodeProgress => encodeProgress.FileName)
				.ToProperty(this, x => x.FileName, out this.fileName);

			this.GetPropertyWatcher(encodeProgress => encodeProgress.FileProgressFraction * 100)
				.ToProperty(this, x => x.FileProgressPercent, out this.fileProgressPercent);

			this.GetPropertyWatcher(encodeProgress => Utilities.FormatFileSize(encodeProgress.FileSizeBytes))
				.ToProperty(this, x => x.FileSize, out this.fileSize);

            this.GetPropertyWatcher(encodeProgress => Utilities.FormatTimeSpan(encodeProgress.FileElapsedTime))
				.ToProperty(this, x => x.FileElapsedTime, out this.fileElapsedTime);

            this.GetPropertyWatcher(encodeProgress => Utilities.FormatTimeSpan(encodeProgress.FileEta))
				.ToProperty(this, x => x.FileEta, out this.fileEta);

            this.GetPropertyWatcher(encodeProgress => encodeProgress.CurrentFps)
				.ToProperty(this, x => x.CurrentFps, out this.currentFps);

            this.GetPropertyWatcher(encodeProgress => encodeProgress.AverageFps)
				.ToProperty(this, x => x.AverageFps, out this.averageFps);

            this.GetPropertyWatcher(encodeProgress => encodeProgress.HasScanPass || encodeProgress.TwoPass)
				.ToProperty(this, x => x.ShowPassProgress, out this.showPassProgress);

            this.GetPropertyWatcher(encodeProgress => encodeProgress.PassProgressFraction * 100)
				.ToProperty(this, x => x.PassProgressPercent, out this.passProgressPercent);

			this.GetPropertyWatcher(encodeProgress =>
			{
				switch (encodeProgress.CurrentPassId)
				{
					case -1:
						return EncodeDetailsRes.ScanPassLabel;
					case 0:
						return EncodeDetailsRes.EncodePassLabel;
					case 1:
						return EncodeDetailsRes.FirstPassLabel;
					case 2:
						return EncodeDetailsRes.SecondPassLabel;
					default:
						return null;
				}
			}).ToProperty(this, x => x.PassProgressLabel, out this.passProgressLabel);
		}

		private IObservable<T> GetPropertyWatcher<T>(Func<EncodeProgress, T> func)
		{
			return this.progressObservable
				.Select(encodeProgress =>
				{
					if (encodeProgress == null)
					{
						return default(T);
					}

					return func(encodeProgress);
				});
		}

		private ObservableAsPropertyHelper<double> overallProgressPercent;
		public double OverallProgressPercent => this.overallProgressPercent.Value;

		private ObservableAsPropertyHelper<string> taskNumberDisplay;
		public string TaskNumberDisplay => this.taskNumberDisplay.Value;

		private ObservableAsPropertyHelper<string> overallElapsedTime;
		public string OverallElapsedTime => this.overallElapsedTime.Value;

		private ObservableAsPropertyHelper<string> overallEta;
		public string OverallEta => this.overallEta.Value;

		private ObservableAsPropertyHelper<string> fileName;
		public string FileName => this.fileName.Value;

		private ObservableAsPropertyHelper<double> fileProgressPercent;
		public double FileProgressPercent => this.fileProgressPercent.Value;

		private ObservableAsPropertyHelper<string> fileSize;
		public string FileSize => this.fileSize.Value;

		private ObservableAsPropertyHelper<string> fileElapsedTime;
		public string FileElapsedTime => this.fileElapsedTime.Value;

		private ObservableAsPropertyHelper<string> fileEta;
		public string FileEta => this.fileEta.Value;

		private ObservableAsPropertyHelper<double> currentFps;
		public double CurrentFps => this.currentFps.Value;

		private ObservableAsPropertyHelper<double> averageFps;
		public double AverageFps => this.averageFps.Value;

		private ObservableAsPropertyHelper<bool> showPassProgress;
		public bool ShowPassProgress => this.showPassProgress.Value;

		private ObservableAsPropertyHelper<string> passProgressLabel;
		public string PassProgressLabel => this.passProgressLabel.Value;

		private ObservableAsPropertyHelper<double> passProgressPercent;
		public double PassProgressPercent => this.passProgressPercent.Value;
	}
}
