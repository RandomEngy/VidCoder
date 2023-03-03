using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VidCoder.Model;

namespace VidCoder.Services;

public interface IDriveService : IDisposable
{
	IList<DriveInformation> GetDiscInformation();
	IList<DriveInfo> GetDriveInformation();
	bool PathIsDrive(string sourcePath);
	DriveInformation GetDriveInformationFromPath(string sourcePath);
}
