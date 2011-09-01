using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VidCoder.Model;

namespace VidCoder.Services
{
	public interface IDriveService
	{
		IList<DriveInformation> GetDiscInformation();
		void Close();
		IList<DriveInfo> GetDriveInformation();
	}
}
