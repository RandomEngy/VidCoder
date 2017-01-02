using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReactiveUI;

namespace VidCoder.Model
{
	public enum DiscType
	{
		Dvd,
		BluRay
	}

	public class DriveInformation : ReactiveObject
	{
		private bool empty;
		public bool Empty
		{
			get { return this.empty; }
			set { this.RaiseAndSetIfChanged(ref this.empty, value); }
		}

		public string RootDirectory { get; set; }
		public string VolumeLabel { get; set; }
		public DiscType DiscType { get; set; }

		public string DisplayText
		{
			get
			{
				string prefix = this.RootDirectory + " - ";

				if (this.Empty)
				{
					return prefix + "(Empty)";
				}

				return prefix + this.VolumeLabel;
			}
		}
	}
}
