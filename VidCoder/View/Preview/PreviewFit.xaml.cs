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
	public partial class PreviewFit : UserControl, IPreviewFrame
	{
		public PreviewFit()
		{
			this.InitializeComponent();
		}

		public Grid Holder => this.previewHolder;

		public void ResizeHolder(Grid previewArea, double widthPixels, double heightPixels, bool showOneToOneWhenSmaller)
		{
			ImageUtilities.UpdatePreviewHolderSize(this.previewHolder, previewArea, widthPixels, heightPixels, showOneToOneWhenSmaller);
		}
	}
}
