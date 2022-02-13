using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Services
{
	public interface IFileService
	{
		IList<string> GetFileNames(string initialDirectory);
		string GetFileNameLoad(string initialDirectory = null, string title = null, string filter = null);
		string GetFileNameSave(string initialDirectory = null, string title = null, string initialFileName = null, string defaultExt = null, string filter = null);
		string GetFolderName(string initialDirectory);
		string GetFolderName(string initialDirectory, string description);
		void LaunchFile(string fileName);
		void LaunchUrl(string url);
		void PlayVideo(string fileName);
		void ReportBug();
	}
}
