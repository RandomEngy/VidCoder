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
	/// <summary>
	/// Interaction logic for PreviewStillOneToOne.xaml
	/// </summary>
	public partial class PreviewStillOneToOne : UserControl
	{
		public PreviewStillOneToOne()
		{
			this.InitializeComponent();
		}

		public void ResizeHolder(double widthPixels, double heightPixels)
		{
			this.previewImageOneToOne.Width = widthPixels / ImageUtilities.DpiXFactor;
			this.previewImageOneToOne.Height = heightPixels / ImageUtilities.DpiYFactor;
		}
	}
}
