using HandBrake.Interop.Interop.Json.Shared;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using VidCoder.Controls;
using VidCoder.Resources;
using VidCoder.Services.Windows;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class CompareWindowViewModel : ReactiveObject, IClosableWindow
	{
		private DispatcherTimer seekBarUpdateTimer;

		public CompareWindowViewModel()
		{
			this.isMuted = Config.CompareWindowIsMuted;

			this.seekBarUpdateTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(250)
			};
			this.seekBarUpdateTimer.Tick += (sender, args) =>
			{
				if (this.View != null)
				{
					this.View.RefreshViewModelFromMediaElement();
				}
			};
			this.seekBarUpdateTimer.Start();

			// OriginalVsEncoded
			this.WhenAnyValue(x => x.EncodedShowing)
				.Select(encodedShowing =>
				{
					return encodedShowing ? MainRes.CompareEncodedLabel : MainRes.CompareOriginalLabel;
				})
				.ToProperty(this, x => x.OriginalVsEncoded, out this.originalVsEncoded);

			// FileName
			this.WhenAnyValue(x => x.EncodedShowing, x => x.OriginalFilePath, x => x.EncodedFilePath, (encodedShowing, originalFilePath, encodedFilePath) =>
				{
					string filePath = encodedShowing ? encodedFilePath : originalFilePath;
					return Path.GetFileName(filePath);
				})
				.ToProperty(this, x => x.FileName, out this.fileName);

			// Title
			this.WhenAnyValue(x => x.OriginalVsEncoded, x => x.FileName, (originalVsEncoded, fileName) =>
				{
					return string.Format(MainRes.CompareWindowTitleFormat, originalVsEncoded, fileName);
				})
				.ToProperty(this, x => x.Title, out this.title);

			// SizeText
			this.WhenAnyValue(x => x.EncodedShowing, x => x.OriginalSizeBytes, x => x.EncodedSizeBytes, (encodedShowing, originalSizeBytes, encodedSizeBytes) =>
				{
					return Utilities.FormatFileSize(encodedShowing ? encodedSizeBytes : originalSizeBytes);
				})
				.ToProperty(this, x => x.SizeText, out this.sizeText);

			// SeekBarColor
			this.WhenAnyValue(x => x.EncodedShowing)
				.Select(encodedShowing => encodedShowing ? VideoSeekBar.SeekBarColorChoice.Green : VideoSeekBar.SeekBarColorChoice.Blue)
				.ToProperty(this, x => x.SeekBarColor, out this.seekBarColor);
		}

		public ICompareView View { get; set; }

		private ObservableAsPropertyHelper<string> originalVsEncoded;
		public string OriginalVsEncoded => this.originalVsEncoded.Value;

		private ObservableAsPropertyHelper<string> fileName;
		public string FileName => this.fileName.Value;

		private ObservableAsPropertyHelper<string> title;
		public string Title => this.title.Value;

		private ObservableAsPropertyHelper<string> sizeText;
		public string SizeText => this.sizeText.Value;

		private ObservableAsPropertyHelper<VideoSeekBar.SeekBarColorChoice> seekBarColor;
		public VideoSeekBar.SeekBarColorChoice SeekBarColor => this.seekBarColor.Value;

		private string originalFilePath;
		public string OriginalFilePath
		{
			get => this.originalFilePath;
			set => this.RaiseAndSetIfChanged(ref this.originalFilePath, value);
		}

		private string encodedFilePath;
		public string EncodedFilePath
		{
			get => this.encodedFilePath;
			set => this.RaiseAndSetIfChanged(ref this.encodedFilePath, value);
		}

		private long originalSizeBytes;
		public long OriginalSizeBytes
		{
			get => this.originalSizeBytes;
			set => this.RaiseAndSetIfChanged(ref this.originalSizeBytes, value);
		}

		private long encodedSizeBytes;
		public long EncodedSizeBytes
		{
			get => this.encodedSizeBytes;
			set => this.RaiseAndSetIfChanged(ref this.encodedSizeBytes, value);
		}

		private bool encodedShowing;
		public bool EncodedShowing
		{
			get => this.encodedShowing;
			set => this.RaiseAndSetIfChanged(ref this.encodedShowing, value);
		}

		private bool positionSetByNonUser;

		private TimeSpan videoPosition;
		public TimeSpan VideoPosition
		{
			get => this.videoPosition;
			set
			{
				this.RaiseAndSetIfChanged(ref this.videoPosition, value);
				if (!this.positionSetByNonUser)
				{
					this.View.SeekToViewModelPosition(value);
				}
			}
		}

		// When the video playback progresses naturally or is reset by system
		public void SetVideoPositionFromNonUser(TimeSpan position)
		{
			this.positionSetByNonUser = true;
			this.VideoPosition = position;
			this.positionSetByNonUser = false;
		}

		public bool OnClosing()
		{
			this.seekBarUpdateTimer.Stop();
			return true;
		}

		private TimeSpan videoDuration;
		public TimeSpan VideoDuration
		{
			get => this.videoDuration;
			set => this.RaiseAndSetIfChanged(ref this.videoDuration, value);
		}

		private bool paused = true;
		public bool Paused
		{
			get => this.paused;
			set => this.RaiseAndSetIfChanged(ref this.paused, value);
		}

		public void TogglePlayPause()
		{
			this.Paused = !this.Paused;
		}

		private ReactiveCommand<Unit, Unit> pause;
		public ICommand Pause
		{
			get
			{
				return this.pause ?? (this.pause = ReactiveCommand.Create(() =>
				{
					this.Paused = true;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> play;
		public ICommand Play
		{
			get
			{
				return this.play ?? (this.play = ReactiveCommand.Create(() =>
				{
					this.Paused = false;
				}));
			}
		}

		private bool isMuted = true;
		public bool IsMuted
		{
			get => this.isMuted;
			set => this.RaiseAndSetIfChanged(ref this.isMuted, value);
		}

		private ReactiveCommand<Unit, Unit> mute;
		public ICommand Mute
		{
			get
			{
				return this.mute ?? (this.mute = ReactiveCommand.Create(() =>
				{
					this.IsMuted = true;
					Config.CompareWindowIsMuted = true;
				}));
			}
		}

		private ReactiveCommand<Unit, Unit> unmute;
		public ICommand Unmute
		{
			get
			{
				return this.unmute ?? (this.unmute = ReactiveCommand.Create(() =>
				{
					this.IsMuted = false;
					Config.CompareWindowIsMuted = false;
				}));
			}
		}

		public void OpenVideos(
			string originalFilePath,
			string encodedFilePath,
			long originalSizeBytes,
			long encodedSizeBytes)
		{
			if (!this.Paused)
			{
				this.Paused = true;
			}

			this.OriginalFilePath = originalFilePath;
			this.EncodedFilePath = encodedFilePath;
			this.OriginalSizeBytes = originalSizeBytes;
			this.EncodedSizeBytes = encodedSizeBytes;
		}

		public void OnVideoEnded()
		{
			this.Paused = true;
			this.SetVideoPositionFromNonUser(TimeSpan.Zero);
		}
	}
}
