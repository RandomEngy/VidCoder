using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using VidCoder.Properties;
using System.IO;

namespace VidCoder.ViewModel
{
    public class PreviewViewModel : OkCancelDialogViewModel
    {
        private const string NoSourceTitle = "Preview: No video source";

        private EncodeJob job;
        private HandBrakeInstance previewInstance;
        private Logger logger;
        private string title;
        private int selectedPreview;
        private bool hasPreview;
        private bool generatingPreview;
        private double previewPercentComplete;
        private int previewSeconds;

        private ICommand generatePreviewCommand;

        private MainViewModel mainViewModel;

        public PreviewViewModel(MainViewModel mainViewModel)
        {
            this.mainViewModel = mainViewModel;
            this.logger = mainViewModel.Logger;
            this.previewSeconds = Settings.Default.PreviewSeconds;
            this.Title = NoSourceTitle;

            this.RefreshPreviews();
        }

        public MainViewModel MainViewModel
        {
            get
            {
                return this.mainViewModel;
            }
        }

        public string Title
        {
            get
            {
                return this.title;
            }

            set
            {
                this.title = value;
                this.NotifyPropertyChanged("Title");
            }
        }

        public ImageSource PreviewSource
        {
            get
            {
                if (this.HasPreview)
                {
                    return this.ScanInstance.GetPreview(this.job, this.SelectedPreview);
                }

                return null;
            }
        }

        public bool GeneratingPreview
        {
            get
            {
                return this.generatingPreview;
            }

            set
            {
                this.generatingPreview = value;
                this.NotifyPropertyChanged("GeneratingPreview");
                this.NotifyPropertyChanged("SliderEnabled");
            }
        }

        public bool SliderEnabled
        {
            get
            {
                return this.HasPreview && !this.GeneratingPreview;
            }
        }

        public double PreviewPercentComplete
        {
            get
            {
                return this.previewPercentComplete;
            }

            set
            {
                this.previewPercentComplete = value;
                this.NotifyPropertyChanged("PreviewPercentComplete");
            }
        }

        public int PreviewSeconds
        {
            get
            {
                return this.previewSeconds;
            }

            set
            {
                this.previewSeconds = value;
                this.NotifyPropertyChanged("PreviewSeconds");

                Settings.Default.PreviewSeconds = value;
                Settings.Default.Save();
            }
        }

        public bool HasPreview
        {
            get
            {
                return this.hasPreview;
            }

            set
            {
                this.hasPreview = value;
                this.NotifyPropertyChanged("SliderEnabled");
                this.NotifyPropertyChanged("HasPreview");
            }
        }

        public int SelectedPreview
        {
            get
            {
                return this.selectedPreview;
            }

            set
            {
                this.selectedPreview = value;
                this.NotifyPropertyChanged("SelectedPreview");
                this.NotifyPropertyChanged("PreviewSource");
            }
        }

        /// <summary>
        /// Gets or sets the width of the preview image in pixels.
        /// </summary>
        public double PreviewWidth { get; set; }

        /// <summary>
        /// Gets or sets the height of the preview image in pixels.
        /// </summary>
        public double PreviewHeight { get; set; }

        public int SliderMax
        {
            get
            {
                return 9;
            }
        }

        public HandBrakeInstance ScanInstance
        {
            get
            {
                return this.mainViewModel.ScanInstance;
            }
        }

        public ICommand GeneratePreviewCommand
        {
            get
            {
                if (this.generatePreviewCommand == null)
                {
                    this.generatePreviewCommand = new RelayCommand(param =>
                    {
                        this.job = this.mainViewModel.EncodeJob;
                        this.logger.Log("## Generating preview clip");
                        this.logger.Log("## Scanning title");


                        this.previewInstance = new HandBrakeInstance();
                        this.previewInstance.Initialize(2);
                        this.previewInstance.ScanCompleted += this.OnPreviewScanCompleted;
                        this.previewInstance.StartScan(this.job.SourcePath, 10, this.job.Title);

                        this.PreviewPercentComplete = 0;
                        this.GeneratingPreview = true;
                    },
                    param =>
                    {
                        return this.HasPreview;
                    });
                }

                return this.generatePreviewCommand;
            }
        }

        public void RefreshPreviews()
        {
            if (!this.mainViewModel.HasVideoSource)
            {
                this.HasPreview = false;
                this.Title = NoSourceTitle;
                return;
            }

            this.job = this.mainViewModel.EncodeJob;

            EncodingProfile profile = this.job.EncodingProfile;
            int width, height;

            if (profile.Anamorphic == Anamorphic.None)
            {
                width = profile.Width;
                if (width == 0)
                {
                    width = this.mainViewModel.SelectedTitle.Resolution.Width;
                }

                if (profile.MaxWidth > 0 && width > profile.MaxWidth)
                {
                    width = profile.MaxWidth;
                }

                height = profile.Height;
                if (height == 0)
                {
                    height = this.mainViewModel.SelectedTitle.Resolution.Height;
                }

                if (profile.MaxHeight > 0 && height > profile.MaxHeight)
                {
                    height = profile.MaxHeight;
                }

                if (profile.KeepDisplayAspect)
                {
                    if (profile.Width == 0 && profile.Height == 0 || profile.Width == 0)
                    {
                        width = (int)((double)height * this.mainViewModel.SelectedTitle.AspectRatio);
                    }
                    else if (profile.Height == 0)
                    {
                        height = (int)((double)width / this.mainViewModel.SelectedTitle.AspectRatio);
                    }
                }

                this.PreviewWidth = width;
                this.PreviewHeight = height;
            }
            else
            {
                int parWidth, parHeight;
                this.ScanInstance.GetAnamorphicSize(this.job, out width, out height, out parWidth, out parHeight);
                this.PreviewHeight = height;
                this.PreviewWidth = width * ((double)parWidth / parHeight);
            }

            this.HasPreview = true;
            this.NotifyPropertyChanged("PreviewSource");
            this.Title = "Preview: Display " + Math.Round(this.PreviewWidth) + "x" + Math.Round(this.PreviewHeight) + " - Storage " + width + "x" + height;
        }

        private void OnPreviewScanCompleted(object sender, EventArgs eventArgs)
        {
            string extension = null;

            if (this.job.EncodingProfile.OutputFormat == OutputFormat.Mkv)
            {
                extension = ".mkv";
            }
            else
            {
                if (this.job.EncodingProfile.PreferredExtension == OutputExtension.M4v)
                {
                    extension = ".m4v";
                }
                else
                {
                    extension = ".mp4";
                }
            }

            string previewDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder");

            if (!Directory.Exists(previewDirectory))
            {
                Directory.CreateDirectory(previewDirectory);
            }

            this.job.OutputPath = Path.Combine(previewDirectory, @"preview" + extension);
            this.previewInstance.EncodeProgress += (o, e) =>
            {
                this.PreviewPercentComplete = e.FractionComplete * 100;
            };
            this.previewInstance.EncodeCompleted += (o, e) =>
            {
                this.GeneratingPreview = false;
                this.logger.Log("# Finished preview clip generation");

                FileService.Instance.LaunchFile(this.job.OutputPath);
            };

            this.logger.Log("## Encoding clip");
            this.logger.Log("##   Path: " + this.job.OutputPath);
            this.logger.Log("##   Title: " + this.job.Title);
            this.logger.Log("##   Preview #: " + this.SelectedPreview);

            this.previewInstance.StartEncode(this.job, true, this.SelectedPreview, this.PreviewSeconds);
        }
    }
}
