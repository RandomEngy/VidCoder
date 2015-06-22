using System;
using System.Reactive.Linq;
using System.Windows.Shell;
using ReactiveUI;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class TaskBarProgressTracker : ReactiveObject
	{
		private readonly ProcessingService processingService;
		private readonly MainViewModel mainViewModel;

		public TaskBarProgressTracker()
		{
			this.processingService = Ioc.Container.GetInstance<ProcessingService>();
			this.mainViewModel = Ioc.Container.GetInstance<MainViewModel>();

			// Set up some observables for properties we care about.
			var isEncodingObservable = this.processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Select(encodeProgress => encodeProgress != null && encodeProgress.Encoding);

			var encodeProgressFractionObservable = this.processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Select(encodeProgress => encodeProgress == null ? 0 : encodeProgress.OverallProgressFraction);

			var isEncodePausedObservable = this.processingService.WhenAnyValue(x => x.Paused);
			var isScanningObservable = this.mainViewModel.WhenAnyValue(x => x.ScanningSource);
			var scanProgressFractionObservable = this.mainViewModel.WhenAnyValue(x => x.ScanProgressFraction);

			// Set up output properties
			Observable.CombineLatest(
				isEncodingObservable,
				encodeProgressFractionObservable,
				isScanningObservable,
				scanProgressFractionObservable,
				(isEncoding, encodeProgressFraction, isScanning, scanProgressFraction) =>
				{
					if (isEncoding)
					{
						return encodeProgressFraction;
					}
					else if (isScanning)
					{
						return scanProgressFraction;
					}
					else
					{
						return 0;
					}
				}).ToProperty(this, x => x.ProgressFraction, out this.progressFraction);

			Observable.CombineLatest(
				isEncodingObservable,
				isEncodePausedObservable,
				isScanningObservable,
				(isEncoding, isEncodePaused, isScanning) =>
				{
					if (isEncoding)
					{
						if (isEncodePaused)
						{
							return TaskbarItemProgressState.Paused;
						}
						else
						{
							return TaskbarItemProgressState.Normal;
						}
					}
					else if (isScanning)
					{
						return TaskbarItemProgressState.Normal;
					}
					else
					{
						return TaskbarItemProgressState.None;
					}
				}).ToProperty(this, x => x.ProgressState, out this.progressState);
		}

		private ObservableAsPropertyHelper<double> progressFraction;
		public double ProgressFraction
		{
			get { return this.progressFraction.Value; }
		}

		private ObservableAsPropertyHelper<TaskbarItemProgressState> progressState;
		public TaskbarItemProgressState ProgressState
		{
			get { return this.progressState.Value; }
		}
	}
}
