using System;
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

			// Pipe some properties in as local properties
			this.processingService
				.WhenAnyValue(x => x.EncodeProgress)
				.Subscribe(encodeProgress =>
				{
					this.IsEncoding = encodeProgress != null && encodeProgress.Encoding;
					this.EncodeProgressFraction = encodeProgress == null ? 0 : encodeProgress.OverallProgressFraction;
				});

			this.processingService
				.WhenAnyValue(x => x.Paused)
				.Subscribe(paused =>
				{
					this.IsEncodePaused = paused;
				});

			this.mainViewModel
				.WhenAnyValue(x => x.ScanProgressFraction)
				.Subscribe(scanProgressFraction =>
				{
					this.ScanProgressFraction = scanProgressFraction;
				});

			this.mainViewModel
				.WhenAnyValue(x => x.ScanningSource)
				.Subscribe(scanningSource =>
				{
					this.IsScanning = scanningSource;
				});

			// Set up output properties
			this.WhenAnyValue(
				x => x.IsEncoding,
				x => x.EncodeProgressFraction,
				x => x.IsScanning,
				x => x.ScanProgressFraction,
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

			this.WhenAnyValue(
				x => x.IsEncoding,
				x => x.IsEncodePaused,
				x => x.IsScanning,
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

		private bool isEncoding;
		public bool IsEncoding
		{
			get { return this.isEncoding; }
			set { this.RaiseAndSetIfChanged(ref this.isEncoding, value); }
		}

		private double encodeProgressFraction;
		public double EncodeProgressFraction
		{
			get { return this.encodeProgressFraction; }
			set { this.RaiseAndSetIfChanged(ref this.encodeProgressFraction, value); }
		}

		private bool isEncodePaused;
		public bool IsEncodePaused
		{
			get { return this.isEncodePaused; }
			set { this.RaiseAndSetIfChanged(ref this.isEncodePaused, value); }
		}

		private bool isScanning;
		public bool IsScanning
		{
			get { return this.isScanning; }
			set { this.RaiseAndSetIfChanged(ref this.isScanning, value); }
		}

		private double scanProgressFraction;
		public double ScanProgressFraction
		{
			get { return this.scanProgressFraction; }
			set { this.RaiseAndSetIfChanged(ref this.scanProgressFraction, value); }
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
