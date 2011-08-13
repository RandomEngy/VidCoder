using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.View;
using Microsoft.Practices.Unity;

namespace VidCoder.Services
{
	public class TrayService
	{
		public void ShowBalloonMessage(string title, string message)
		{
			Unity.Container.Resolve<MainWindow>().ShowBalloonMessage(title, message);
		}
	}
}
