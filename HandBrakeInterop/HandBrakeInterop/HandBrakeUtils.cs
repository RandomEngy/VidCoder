using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HandBrake.Interop
{
	public static class HandBrakeUtils
	{
		/// <summary>
		/// The callback for log messages from HandBrake.
		/// </summary>
		private static LoggingCallback loggingCallback;

		/// <summary>
		/// The callback for error messages from HandBrake.
		/// </summary>
		private static LoggingCallback errorCallback;

		/// <summary>
		/// Fires when HandBrake has logged a message.
		/// </summary>
		public static event EventHandler<MessageLoggedEventArgs> MessageLogged;

		/// <summary>
		/// Fires when HandBrake has logged an error.
		/// </summary>
		public static event EventHandler<MessageLoggedEventArgs> ErrorLogged;

		public static void RegisterLogger()
		{
			// Register the logger if we have not already
			if (loggingCallback == null)
			{
				// Keep the callback as a member to prevent it from being garbage collected.
				loggingCallback = new LoggingCallback(LoggingHandler);
				errorCallback = new LoggingCallback(ErrorHandler);
				HbLib.hb_register_logger(loggingCallback);
				HbLib.hb_register_error_handler(errorCallback);
			}
		}

		/// <summary>
		/// Handles log messages from HandBrake.
		/// </summary>
		/// <param name="message">The log message (including newline).</param>
		public static void LoggingHandler(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				string[] messageParts = message.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

				if (messageParts.Length > 0)
				{
					if (MessageLogged != null)
					{
						MessageLogged(null, new MessageLoggedEventArgs { Message = messageParts[0] });
					}

					System.Diagnostics.Debug.WriteLine(messageParts[0]);
				}
			}
		}

		/// <summary>
		/// Handles errors from HandBrake.
		/// </summary>
		/// <param name="message">The error message.</param>
		public static void ErrorHandler(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				if (ErrorLogged != null)
				{
					ErrorLogged(null, new MessageLoggedEventArgs { Message = message });
				}

				System.Diagnostics.Debug.WriteLine("ERROR: " + message);
			}
		}

		/// <summary>
		/// Gets the bitrate limits for the given audio codec, sample rate and mixdown.
		/// </summary>
		/// <param name="codec">The audio codec used.</param>
		/// <param name="sampleRate">The sample rate used (Hz).</param>
		/// <param name="mixdown">The mixdown used.</param>
		/// <returns>Limits on the audio bitrate for the given settings.</returns>
		public static Limits GetBitrateLimits(AudioEncoder codec, int sampleRate, Mixdown mixdown)
		{
			if (mixdown == Mixdown.Auto)
			{
				throw new ArgumentException("Mixdown cannot be Auto.");			
			}

			int low = 0;
			int high = 0;

			HbLib.hb_get_audio_bitrate_limits(Converters.AudioCodecToNative(codec), sampleRate, Converters.MixdownToNative(mixdown), ref low, ref high);

			return new Limits { Low = low, High = high };
		}
	}
}
