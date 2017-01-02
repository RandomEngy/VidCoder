using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.View;

namespace VidCoder.Services
{
	public class TrayService
	{
		public void ShowBalloonMessage(string title, string message)
		{
			Ioc.Get<Main>().ShowBalloonMessage(title, message);
		}
	}
}
