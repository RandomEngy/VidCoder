using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AnyContainer;
using VidCoder.View;

namespace VidCoder.Services
{
	public class TrayService
	{
		public void ShowBalloonMessage(string title, string message)
		{
			StaticResolver.Resolve<Main>().ShowBalloonMessage(title, message);
		}
	}
}
