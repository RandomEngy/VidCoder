using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace VidCoder.Model
{
	public class SaveImageJob
	{
		public int PreviewNumber { get; set; }

		public int UpdateVersion { get; set; }

		public string FilePath { get; set; }

		public BitmapSource Image { get; set; }

		/// <summary>
		/// Gets or sets the object to lock on before accessing the file cache image.
		/// </summary>
		public object ImageFileSync { get; set; }
	}
}
