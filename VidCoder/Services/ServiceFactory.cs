using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.ViewModel;

namespace VidCoder.Services
{
    public static class ServiceFactory
    {
        public static IDriveService CreateDriveService(MainViewModel mainViewModel)
        {
            return new DriveService(mainViewModel);
        }

        public static IUpdateService UpdateService
        {
            get
            {
                return new UpdateService();
            }
        }

        public static IMessageBoxService MessageBoxService
        {
            get
            {
                return new MessageBoxService();
            }
        }
    }
}
