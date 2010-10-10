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
			WqlEventQuery query;
			ManagementOperationObserver observer = new ManagementOperationObserver();

			// Bind to local machine
			ConnectionOptions options = new ConnectionOptions();
			options.EnablePrivileges = true; //sets required privilege
			ManagementScope scope = new ManagementScope(@"root\CIMV2", options);

			try
			{
				query = new WqlEventQuery();
				query.EventClassName = "__InstanceModificationEvent";
				query.WithinInterval = TimeSpan.FromSeconds(1);

				// DriveType - 5: CDROM
				query.Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5";
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

		// Dump all properties
		private void HandleDiscEvent(object sender, EventArrivedEventArgs e)
		{
			this.mainViewModel.UpdateDriveCollection();

			// Get the Event object and display it
			PropertyData pd = e.NewEvent.Properties["TargetInstance"];

			if (pd != null)
			{
				ManagementBaseObject mbo = pd.Value as ManagementBaseObject;

				// if CD removed VolumeName == null
				if (mbo.Properties["VolumeName"].Value != null)
				{
					System.Diagnostics.Debug.WriteLine("CD has been inserted");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("CD has been ejected");
				}
			}
		}


		public IList<DriveInformation> GetDriveInformation()
		{
			DriveInfo[] driveCollection = DriveInfo.GetDrives();
			List<DriveInformation> driveList = new List<DriveInformation>();

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

		public void Close()
		{
			this.watcher.Stop();
		}
	}
}
