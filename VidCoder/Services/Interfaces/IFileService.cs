using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder
{
    public interface IFileService
    {
        string GetFileNameLoad(string defaultExt, string filter, string initialDirectory);
        string GetFileNameSave(string initialDirectory);
        string GetFolderName(string initialDirectory);
        void LaunchFile(string fileName);
    }
}
