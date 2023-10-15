using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VidCoder.Controls;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.Extensions;

public static class PreviewFrameExtensions
{
	public static void SetImage(this IPreviewFrame previewFrame)
	{
		Grid holder = previewFrame.Holder;

		if (HasChild<PaddingFrame>(holder))
		{
			// There is already an image on here.
			RemoveVideo(holder);
			return;
		}

		RemoveChildren(holder);

		PreviewWindowViewModel viewModel;

		var image = new Image();
		image.SetBinding(Image.SourceProperty, nameof(viewModel.PreviewImageServiceClient) + "." + nameof(viewModel.PreviewImageServiceClient.PreviewImage));
		previewFrame.Holder.Children.Add(image);
	}

	public static void SetVideo(this IPreviewFrame previewFrame, Action onCompleted, Action onFailed, IObservable<double> volume)
	{
		Grid holder = previewFrame.Holder;

		if (HasChild<MediaElement>(holder))
		{
			// There is already a video in here.
			RemoveImage(holder);
			return;
		}

		// Close any video that was there before.
		RemoveVideo(holder);

		// Add on a new video
		var mediaElement = new MediaElement
		{
			Stretch = Stretch.Fill,
			Opacity = 0
		};

		var viewModel = (PreviewWindowViewModel) previewFrame.Holder.DataContext;
		mediaElement.SetBinding(MediaElement.SourceProperty, nameof(viewModel.PreviewFilePath));
		mediaElement.LoadedBehavior = MediaState.Manual;
		mediaElement.ScrubbingEnabled = true;
		mediaElement.MediaOpened += (sender, args) =>
		{
			if (mediaElement.NaturalDuration.HasTimeSpan)
			{
				viewModel.PreviewVideoDuration = mediaElement.NaturalDuration.TimeSpan;
			}
			else
			{
				viewModel.PreviewVideoDuration = TimeSpan.Zero;
			}

			// Video starts out black so we want to hold it at the start until it displays fully.
			mediaElement.Pause();

			DispatchUtilities.BeginInvoke(() =>
			{
				OnVideoPlaying(holder);
				mediaElement.Play();
			});
		};

		mediaElement.MediaFailed += (sender, args) =>
		{
			onFailed();
		};

		mediaElement.MediaEnded += (sender, args) =>
		{
			onCompleted();
		};

		volume.Subscribe(v =>
		{
			mediaElement.Volume = v;
		});

		mediaElement.Play();

		holder.Children.Add(mediaElement);
	}

	public static TimeSpan GetVideoPosition(this IPreviewFrame previewFrame)
	{
		return RunOnVideo(previewFrame.Holder, mediaElement =>
		{
			return mediaElement.Position;
		});
	}

	public static void SetVideoPosition(this IPreviewFrame previewFrame, TimeSpan position)
	{
		RunOnVideo(previewFrame.Holder, mediaElement =>
		{
			mediaElement.Position = position;
		});
	}

	public static void Pause(this IPreviewFrame previewFrame)
	{
		RunOnVideo(previewFrame.Holder, mediaElement =>
		{
			if (mediaElement.CanPause)
			{
				mediaElement.Pause();
			}
		});
	}

	public static void Play(this IPreviewFrame previewFrame)
	{
		RunOnVideo(previewFrame.Holder, mediaElement =>
		{
			mediaElement.Play();
		});
	}

	public static void Close(this IPreviewFrame previewFrame)
	{
		RemoveChildren(previewFrame.Holder);
	}

	public static void CloseVideo(this IPreviewFrame previewFrame)
	{
		RemoveVideo(previewFrame.Holder);
	}

	private static void RunOnVideo(Grid holder, Action<MediaElement> action)
	{
		var mediaElement = GetChild<MediaElement>(holder);
		if (mediaElement == null)
		{
			return;
		}

		action(mediaElement);
	}

	private static T RunOnVideo<T>(Grid holder, Func<MediaElement, T> func)
	{
		var mediaElement = GetChild<MediaElement>(holder);
		if (mediaElement == null)
		{
			return default(T);
		}

		return func(mediaElement);
	}

	private static bool HasChild<T>(Grid holder)
	{
		foreach (UIElement child in holder.Children)
		{
			if (child.GetType() == typeof (T))
			{
				return true;
			}
		}

		return false;
	}

	private static T GetChild<T>(Grid holder) where T : class
	{
		foreach (UIElement child in holder.Children)
		{
			var typedChild = child as T;
			if (typedChild != null)
			{
				return typedChild;
			}
		}

		return null;
	}

	private static void RemoveImage(Grid holder)
	{
		for (int i = holder.Children.Count - 1; i >= 0; i--)
		{
			UIElement child = holder.Children[i];
			var image = child as PaddingFrame;
			if (image != null)
			{
				holder.Children.RemoveAt(i);
			}
		}
	}

	private static void RemoveVideo(Grid holder)
	{
		for (int i = holder.Children.Count - 1; i >= 0; i--)
		{
			UIElement child = holder.Children[i];
			var video = child as MediaElement;
			if (video != null)
			{
				video.Close();
				holder.Children.RemoveAt(i);
			}
		}
	}

	private static void OnVideoPlaying(Grid holder)
	{
		MediaElement video = GetChild<MediaElement>(holder);
		if (video == null)
		{
			return;
		}

		video.Opacity = 1;
		
		RemoveImage(holder);
	}

	private static void RemoveChildren(Grid holder)
	{
		for (int i = holder.Children.Count - 1; i >= 0; i--)
		{
			UIElement child = holder.Children[i];
			var video = child as MediaElement;
			video?.Close();

			holder.Children.RemoveAt(i);
		}
	}
}
