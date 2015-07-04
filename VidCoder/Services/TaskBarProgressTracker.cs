using System;
using System.Reactive.Linq;
using System.Windows.Shell;
using ReactiveUI;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
	public class TaskBarProgressTracker : ReactiveObject
	{
		public TaskBarProgressTracker()
		{
			ProcessingService processingService = Ioc.Get<ProcessingService>();
			MainViewModel mainViewModel = Ioc.Get<MainViewModel>();

			// Set up some observables for properties we care about.
			var isEncodingObservable = processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Select(encodeProgress => encodeProgress != null && encodeProgress.Encoding);

			var encodeProgressFractionObservable = processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Select(encodeProgress => encodeProgress == null ? 0 : encodeProgress.OverallProgressFraction);

			var isEncodePausedObservable = processingService.WhenAnyValue(x => x.Paused);
			var isScanningObservable = mainViewModel.WhenAnyValue(x => x.ScanningSource);
			var scanProgressFractionObservable = mainViewModel.WhenAnyValue(x => x.ScanProgressFraction);

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
