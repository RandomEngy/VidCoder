using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VidCoder.Model;
using VidCoder.ViewModel;

namespace VidCoder.Extensions
{
	public static class PreviewHolderExtensions
	{
		public static void SetImage(this IPreviewHolder previewHolder)
		{
			Grid holder = previewHolder.Holder;

			if (HasChild<Image>(holder))
			{
				// There is already an image on here.
				RemoveVideo(holder);
				return;
			}

			RemoveChildren(holder);

			var image = new Image
			{
				Stretch = Stretch.Fill
			};

			var viewModel = (PreviewWindowViewModel) previewHolder.Holder.DataContext;
			image.SetBinding(Image.SourceProperty, nameof(viewModel.PreviewImage));

			previewHolder.Holder.Children.Add(image);
		}

		public static void SetVideo(this IPreviewHolder previewHolder, Action onCompleted)
		{
			Grid holder = previewHolder.Holder;

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

			var viewModel = (PreviewWindowViewModel) previewHolder.Holder.DataContext;
			mediaElement.SetBinding(MediaElement.SourceProperty, nameof(viewModel.PreviewFilePath));
			mediaElement.LoadedBehavior = MediaState.Manual;
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

			mediaElement.MediaEnded += (sender, args) =>
			{
				onCompleted();
			};

			mediaElement.Play();

			holder.Children.Add(mediaElement);
		}

		public static TimeSpan GetVideoPosition(this IPreviewHolder previewHolder)
		{
			return RunOnVideo(previewHolder.Holder, mediaElement =>
			{
				return mediaElement.Position;
			});
		}

		public static void SetVideoPosition(this IPreviewHolder previewHolder, TimeSpan position)
		{
			RunOnVideo(previewHolder.Holder, mediaElement =>
			{
				mediaElement.Position = position;
			});
		}

		public static void Pause(this IPreviewHolder previewHolder)
		{
			RunOnVideo(previewHolder.Holder, mediaElement =>
			{
				if (mediaElement.CanPause)
				{
					mediaElement.Pause();
				}
			});
		}

		public static void Play(this IPreviewHolder previewHolder)
		{
			RunOnVideo(previewHolder.Holder, mediaElement =>
			{
				mediaElement.Play();
			});
		}

		public static void Close(this IPreviewHolder previewHolder)
		{
			RemoveChildren(previewHolder.Holder);
		}

		public static void CloseVideo(this IPreviewHolder previewHolder)
		{
			RemoveVideo(previewHolder.Holder);
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
				var image = child as Image;
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
}
