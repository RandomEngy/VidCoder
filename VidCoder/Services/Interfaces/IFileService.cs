using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services
{
	public interface IFileService
	{
		IList<string> GetFileNames(string initialDirectory);
		string GetFileNameLoad(string defaultExt, string filter, string initialDirectory);
		string GetFileNameSave(string initialDirectory);
		string GetFolderName(string initialDirectory);
		string GetFolderName(string initialDirectory, string description);
		void LaunchFile(string fileName);
		void LaunchUrl(string url);
	}
}
