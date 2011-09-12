using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HandBrake.Interop.Model;
using VidCoder.Model;
using Microsoft.Practices.Unity;

namespace VidCoder.ViewModel
{
	public class SourceOptionViewModel : ViewModelBase
	{
		private SourceOption sourceOption;
		private ICommand chooseSourceCommand;

		public SourceOptionViewModel(SourceOption sourceOption, string sourcePath = null)
		{
			this.sourceOption = sourceOption;
			this.SourcePath = sourcePath;
		}

		public string Image
		{
			get
			{
				switch (this.sourceOption.Type)
				{
					case SourceType.File:
						return "/Icons/video-file.png";
					case SourceType.VideoFolder:
						return "/Icons/folder.png";
					case SourceType.Dvd:
						if (this.sourceOption.DriveInfo.DiscType == DiscType.Dvd)
						{
							return "/Icons/disc.png";
						}
						else
						{
							return "/Icons/bludisc.png";
						}
					default:
						break;
				}

				return null;
			}
		}

		public bool ImageVisible
		{
			get
			{
				return this.sourceOption.Type != SourceType.None;
			}
		}

		public string Text
		{
			get
			{
				switch (this.sourceOption.Type)
				{
					case SourceType.File:
						if (this.SourcePath == null)
						{
							return "Video File...";
						}

						return this.SourcePath;
					case SourceType.VideoFolder:
						if (this.SourcePath == null)
						{
							return "DVD/Blu-ray Folder...";
						}

						return this.SourcePath;
					case SourceType.Dvd:
						return this.sourceOption.DriveInfo.RootDirectory + " - " + this.sourceOption.DriveInfo.VolumeLabel;
					default:
						break;
				}

				return string.Empty;
			}
		}

		public string SourcePath { get; set; }

		public string VolumeLabel
		{
			get
			{
				return this.sourceOption.DriveInfo.VolumeLabel;
			}

			set
			{
				this.sourceOption.DriveInfo.VolumeLabel = value;
				this.NotifyPropertyChanged("VolumeLabel");
				this.NotifyPropertyChanged("Text");
			}
		}

		public SourceOption SourceOption
		{
			get { return this.sourceOption; }
		}

		public ICommand ChooseSourceCommand
		{
			get
			{
				if (this.chooseSourceCommand == null)
				{
					this.chooseSourceCommand = new RelayCommand(param =>
					{
						var mainVM = Unity.Container.Resolve<MainViewModel>();

						switch (this.SourceOption.Type)
						{
							case SourceType.File:
								if (this.SourcePath == null)
								{
									mainVM.SetSourceFromFile();
								}
								else
								{
									mainVM.SetSourceFromFile(this.SourcePath);
								}
								break;
							case SourceType.VideoFolder:
								if (this.SourcePath == null)
								{
									mainVM.SetSourceFromFolder();
								}
								else
								{
									mainVM.SetSourceFromFolder(this.SourcePath);
								}
								break;
							case SourceType.Dvd:
								mainVM.SetSourceFromDvd(this.SourceOption.DriveInfo);
								break;
							default:
								break;
						}
					});
				}

				return this.chooseSourceCommand;
			}
		}
	}
}
