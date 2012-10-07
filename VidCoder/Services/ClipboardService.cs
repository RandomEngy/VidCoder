using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace VidCoder.Services
{
	using LocalResources;

	public class ClipboardService
	{
		public virtual void SetText(string text)
		{
			for (int i = 0; i < 5; i++)
			{
				try
				{
					Clipboard.SetText(text);
					return;
				}
				catch (COMException)
				{
					// retry
				}
			}

			MessageBox.Show(MainRes.CouldNotCopyTextError);
		}
	}
}
