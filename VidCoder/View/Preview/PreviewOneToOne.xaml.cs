using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VidCoder.Model;

namespace VidCoder.View.Preview
{
	public partial class PreviewOneToOne : UserControl, IPreviewFrame
	{
		public PreviewOneToOne()
		{
			this.InitializeComponent();
		}

		public Grid Holder => this.previewHolder;

		public void ResizeHolder(double widthPixels, double heightPixels)
		{
			this.previewHolder.Width = widthPixels / ImageUtilities.DpiXFactor;
			this.previewHolder.Height = heightPixels / ImageUtilities.DpiYFactor;
		}
	}
}
