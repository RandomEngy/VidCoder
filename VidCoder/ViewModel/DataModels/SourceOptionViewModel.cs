using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using VidCoder.Model;
using HandBrake.Interop;

namespace VidCoder.ViewModel
{
	public class SourceOptionViewModel : ViewModelBase
	{
		private SourceOption sourceOption;

		public SourceOptionViewModel(SourceOption sourceOption)
		{
			this.sourceOption = sourceOption;
		}

		public string Image
		{
			get
			{
				return this.sourceOption.Image;
			}

			set
			{
				this.sourceOption.Image = value;
				this.NotifyPropertyChanged("Image");
			}
		}

		public string Text
		{
			get
			{
				return this.sourceOption.Text;
			}

			set
			{
				this.sourceOption.Text = value;
				this.NotifyPropertyChanged("Text");
				this.NotifyPropertyChanged("Display");
			}
		}

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
				this.NotifyPropertyChanged("Display");
			}
		}

		public string Display
		{
			get
			{
				if (this.sourceOption.Type == SourceType.Dvd)
				{
					return this.sourceOption.DriveInfo.RootDirectory + " - " + this.sourceOption.DriveInfo.VolumeLabel;
				}

				return this.Text;
			}
		}

		public SourceOption SourceOption
		{
			get { return this.sourceOption; }
		}

		public bool ImageVisible
		{
			get
			{
				return !string.IsNullOrEmpty(this.sourceOption.Image);
			}
		}
	}
}
