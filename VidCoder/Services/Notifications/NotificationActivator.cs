using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnyContainer;
using VidCoder.View;

namespace VidCoder.Services.Notifications
{
	/// <summary>
	/// Apps must implement this activator to handle notification activation.
	/// </summary>
	public abstract class NotificationActivator : NotificationActivator.INotificationActivationCallback
	{
		public void Activate(string appUserModelId, string invokedArgs, NOTIFICATION_USER_INPUT_DATA[] data, uint dataCount)
		{
			Resolver.Resolve<Main>().RestoreWindow();

			DispatchUtilities.BeginInvoke(() =>
			{
				Resolver.Resolve<Main>().Activate();
			});
		}

		// These are the new APIs for Windows 10
		#region NewAPIs
		[StructLayout(LayoutKind.Sequential), Serializable]
		public struct NOTIFICATION_USER_INPUT_DATA
		{
			[MarshalAs(UnmanagedType.LPWStr)]
			public string Key;

			[MarshalAs(UnmanagedType.LPWStr)]
			public string Value;
		}

		[ComImport]
		[Guid("53E31837-6600-4A81-9395-75CFFE746F94")]
		[ComVisible(true)]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		public interface INotificationActivationCallback
		{
			void Activate(
				[In, MarshalAs(UnmanagedType.LPWStr)]
				string appUserModelId,
				[In, MarshalAs(UnmanagedType.LPWStr)]
				string invokedArgs,
				[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
				NOTIFICATION_USER_INPUT_DATA[] data,
				[In, MarshalAs(UnmanagedType.U4)]
				uint dataCount);
		}
		#endregion
	}

	/// <summary>
	/// Text and selection values that the user entered on your notification. The Key is the ID of the input, and the Value is what the user entered.
	/// </summary>
	public class NotificationUserInput : IReadOnlyDictionary<string, string>
	{
		private NotificationActivator.NOTIFICATION_USER_INPUT_DATA[] _data;

		internal NotificationUserInput(NotificationActivator.NOTIFICATION_USER_INPUT_DATA[] data)
		{
			_data = data;
		}

		public string this[string key] => _data.First(i => i.Key == key).Value;

		public IEnumerable<string> Keys => _data.Select(i => i.Key);

		public IEnumerable<string> Values => _data.Select(i => i.Value);

		public int Count => _data.Length;

		public bool ContainsKey(string key)
		{
			return _data.Any(i => i.Key == key);
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return _data.Select(i => new KeyValuePair<string, string>(i.Key, i.Value)).GetEnumerator();
		}

		public bool TryGetValue(string key, out string value)
		{
			foreach (var item in _data)
			{
				if (item.Key == key)
				{
					value = item.Value;
					return true;
				}
			}

			value = null;
			return false;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
