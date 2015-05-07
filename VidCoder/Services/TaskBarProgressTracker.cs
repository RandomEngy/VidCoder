using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Shell;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using VidCoder.Messages;

namespace VidCoder.Services
{
	public class TaskBarProgressTracker : ObservableObject
	{
		private bool isEncoding;
		private double encodeProgressFraction;
		private bool isEncodePaused;
		private bool isScanning;
		private double scanProgressFraction;

		public TaskBarProgressTracker()
		{
			Messenger.Default.Register<ProgressChangedMessage>(
				this,
				message =>
				{
					this.isEncoding = message.Encoding;
					this.encodeProgressFraction = message.OverallProgressFraction;
					this.RefreshProgress();
				});

			Messenger.Default.Register<PauseChangedMessage>(
				this,
				message =>
				{
					this.isEncodePaused = message.IsPaused;
					this.RefreshProgress();
				});
		}

		public void SetScanProgress(double newScanProgressFraction)
		{
			this.scanProgressFraction = newScanProgressFraction;
			this.RefreshProgress();
		}

		public void SetIsScanning(bool newIsScanning)
		{
			this.isScanning = newIsScanning;
			this.RefreshProgress();
		}

		private void RefreshProgress()
		{
			this.RaisePropertyChanged(() => this.ProgressState);
			this.RaisePropertyChanged(() => this.ProgressFraction);
		}

		public double ProgressFraction
		{
			get
			{
				if (this.isEncoding)
				{
					return this.encodeProgressFraction;
				}
				else if (this.isScanning)
				{
					return this.scanProgressFraction;
				}
				else
				{
					return 0;
				}
			}
		}

		public TaskbarItemProgressState ProgressState
		{
			get
			{
				if (this.isEncoding)
				{
					if (this.isEncodePaused)
					{
						return TaskbarItemProgressState.Paused;
					}
					else
					{
						return TaskbarItemProgressState.Normal;
					}
				}
				else if (this.isScanning)
				{
					return TaskbarItemProgressState.Normal;
				}
				else
				{
					return TaskbarItemProgressState.None;
				}
			}
		}
	}
}
