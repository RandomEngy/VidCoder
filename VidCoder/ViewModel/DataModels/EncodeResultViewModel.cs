using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using System.Globalization;
using System.Windows.Input;
using System.IO;

namespace VidCoder.ViewModel
{
    public class EncodeResultViewModel
    {
        private MainViewModel mainViewModel;

        private EncodeResult encodeResult;

        private ICommand playCommand;
        private ICommand openContainingFolderCommand;

        public EncodeResultViewModel(EncodeResult result, MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
            this.encodeResult = result;
        }

        public MainViewModel MainViewModel
        {
            get
            {
                return this.mainViewModel;
            }
        }

        public EncodeResult EncodeResult
        {
            get
            {
                return this.encodeResult;
            }
        }

        public string StatusText
        {
            get
            {
                if (this.encodeResult.Succeeded)
                {
                    return "Succeeded";
                }

                return "Failed";
            }
        }

        public string TimeDisplay
        {
            get
            {
                return this.encodeResult.EncodeTime.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
            }
        }

        public string StatusImage
        {
            get
            {
                if (this.encodeResult.Succeeded)
                {
                    return "/Icons/succeeded.png";
                }

                return "/Icons/failed.png";
            }
        }

        public ICommand PlayCommand
        {
            get
            {
                if (this.playCommand == null)
                {
                    this.playCommand = new RelayCommand(param =>
                    {
                        FileService.Instance.LaunchFile(this.encodeResult.Destination);
                    });
                }

                return this.playCommand;
            }
        }

        public ICommand OpenContainingFolderCommand
        {
            get
            {
                if (this.openContainingFolderCommand == null)
                {
                    this.openContainingFolderCommand = new RelayCommand(param =>
                    {
                        FileService.Instance.LaunchFile(Path.GetDirectoryName(this.encodeResult.Destination));
                    });
                }

                return this.openContainingFolderCommand;
            }
        }
    }
}
