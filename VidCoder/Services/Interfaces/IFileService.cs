using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services
{
	public interface IFileService
	{
		IList<string> GetFileNames(string initialDirectory);
		string GetFileNameLoad(string initialDirectory, string defaultExt, string filter);
		string GetFileNameSave(string initialDirectory);
		string GetFileNameSave(string initialDirectory, string initialFileName, string defaultExt, string filter);
		string GetFolderName(string initialDirectory);
		string GetFolderName(string initialDirectory, string description);
		void LaunchFile(string fileName);
		void LaunchUrl(string url);
	}
}
