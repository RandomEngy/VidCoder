using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using VidCoder.ViewModel;
using System.IO;
using System.Management;
using Microsoft.Practices.Unity;

namespace VidCoder.Services
{
	public class DriveService : IDriveService
	{
		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();
		private ManagementEventWatcher watcher;

		public DriveService()
		{
			// Bind to local machine
			var options = new ConnectionOptions { EnablePrivileges = true };
			var scope = new ManagementScope(@"root\CIMV2", options);

			try
			{
				var query = new WqlEventQuery
				{
					EventClassName = "__InstanceModificationEvent",
					WithinInterval = TimeSpan.FromSeconds(1),
					Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5" // DriveType - 5: CDROM
				};

				this.watcher = new ManagementEventWatcher(scope, query);

				// register async. event handler
				this.watcher.EventArrived += this.HandleDiscEvent;
				this.watcher.Start();
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
			}
		}

		private void HandleDiscEvent(object sender, EventArrivedEventArgs e)
		{
			DispatchService.BeginInvoke(() => this.mainViewModel.UpdateDriveCollection());
		}

		public IList<DriveInformation> GetDiscInformation()
		{
			DriveInfo[] driveCollection = DriveInfo.GetDrives();
			var driveList = new List<DriveInformation>();

			foreach (DriveInfo driveInfo in driveCollection)
			{
				if (driveInfo.DriveType == DriveType.CDRom && driveInfo.IsReady)
				{
					if (File.Exists(driveInfo.RootDirectory + @"VIDEO_TS\VIDEO_TS.IFO"))
					{
						driveList.Add(new DriveInformation
						{
							RootDirectory = driveInfo.RootDirectory.FullName,
							VolumeLabel = driveInfo.VolumeLabel,
							DiscType = DiscType.Dvd
						});
					}
					else if (Directory.Exists(driveInfo.RootDirectory + "BDMV"))
					{
						driveList.Add(new DriveInformation
						{
							RootDirectory = driveInfo.RootDirectory.FullName,
							VolumeLabel = driveInfo.VolumeLabel,
							DiscType = DiscType.BluRay
						});
					}
				}
			}

			return driveList;
		}

		public IList<DriveInfo> GetDriveInformation()
		{
			return new List<DriveInfo>(DriveInfo.GetDrives());
		} 

		public void Close()
		{
			this.watcher.Stop();
		}
	}
}
