using Microsoft.AnyContainer;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VidCoder.Services;
using VidCoder.ViewModel;

namespace VidCoder.View;

/// <summary>
/// Interaction logic for CompareWindow.xaml
/// </summary>
public partial class CompareWindow : Window, ICompareView
{
	private bool originalLoaded = false;
	private bool encodedLoaded = false;

	private IDisposable previewPausedSubscription;

	private CompareWindowViewModel viewModel;

	private IAppLogger logger = StaticResolver.Resolve<IAppLogger>();

	public CompareWindow()
	{
		InitializeComponent();

		this.DataContextChanged += this.OnDataContextChanged;
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		this.viewModel = (CompareWindowViewModel)this.DataContext;
		this.viewModel.View = this;

		this.previewPausedSubscription = this.viewModel.WhenAnyValue(x => x.Paused)
			.Subscribe(paused =>
			{
				if (paused)
				{
					this.originalVideo.Pause();
					this.encodedVideo.Pause();
				}
				else
				{
					this.originalVideo.Play();
					this.encodedVideo.Play();
				}
			});
	}

	private void originalVideo_MediaOpened(object sender, RoutedEventArgs e)
	{
		this.originalVideo.Play();
		this.originalVideo.Pause();
		this.originalLoaded = true;
		this.SetPositionIfBothVideosLoaded();
	}

	private void encodedVideo_MediaOpened(object sender, RoutedEventArgs e)
	{
		if (this.encodedVideo.NaturalDuration.HasTimeSpan)
		{
			viewModel.VideoDuration = this.encodedVideo.NaturalDuration.TimeSpan;
		}
		else
		{
			viewModel.VideoDuration = TimeSpan.Zero;
		}

		this.encodedVideo.Play();
		this.encodedVideo.Pause();
		this.encodedLoaded = true;
		this.SetPositionIfBothVideosLoaded();
	}

	private void encodedVideo_MediaEnded(object sender, RoutedEventArgs e)
	{
		this.viewModel.OnVideoEnded();
		this.encodedVideo.Stop();
		this.originalVideo.Stop();
	}

	private void originalVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
	{
		this.logger.LogError("Could not play original video:" + Environment.NewLine + e.ErrorException.ToString());
	}

	private void encodedVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
	{
		this.logger.LogError("Could not play encoded video:" + Environment.NewLine + e.ErrorException.ToString());
	}

	private void SetPositionIfBothVideosLoaded()
	{
		if (this.originalLoaded && this.encodedLoaded)
		{
			TimeSpan startPosition = TimeSpan.FromSeconds(viewModel.VideoDuration.TotalSeconds / 4);
			this.originalVideo.Position = startPosition;
			this.encodedVideo.Position = startPosition;
		}
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.C || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
		{
			this.ShowEncoded();
		}
		else if (e.Key == Key.Escape)
		{
			this.Close();
		}
		else if (e.Key == Key.Space)
		{
			this.viewModel.TogglePlayPause();
		}
	}

	private void Window_KeyUp(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.C || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
		{
			this.ShowOriginal();
		}
	}

	private void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		this.ShowEncoded();
	}

	private void OnMouseUp(object sender, MouseButtonEventArgs e)
	{
		this.ShowOriginal();
	}

	private void ShowEncoded()
	{
		this.originalVideo.Opacity = 0;
		this.encodedVideo.Opacity = 1;

		if (this.viewModel != null)
		{
			this.viewModel.EncodedShowing = true;
		}
	}

	private void ShowOriginal()
	{
		this.originalVideo.Opacity = 1;
		this.encodedVideo.Opacity = 0;

		if (this.viewModel != null)
		{
			this.viewModel.EncodedShowing = false;
		}
	}

	public void RefreshViewModelFromMediaElement()
	{
		TimeSpan position = originalVideo.Position;
		this.viewModel.SetVideoPositionFromNonUser(position);
	}

	public void SeekToViewModelPosition(TimeSpan position)
	{
		this.originalVideo.Position = position;
		this.encodedVideo.Position = position;
	}

	private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
	{
		this.previewPausedSubscription?.Dispose();
	}
}
