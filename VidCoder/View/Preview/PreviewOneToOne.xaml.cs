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
	public partial class PreviewOneToOne : UserControl, IPreviewHolder
	{
		public PreviewOneToOne()
		{
			this.InitializeComponent();
		}

		public UIElement PreviewContent
		{
			get
			{
				if (this.previewHolder.Children.Count == 0)
				{
					return null;
				}

				return this.previewHolder.Children[0];
			}

			set
			{
				this.previewHolder.Children.Clear();
				this.previewHolder.Children.Add(value);
			}
		}

		public void ResizeHolder(double widthPixels, double heightPixels)
		{
			this.previewHolder.Width = widthPixels / ImageUtilities.DpiXFactor;
			this.previewHolder.Height = heightPixels / ImageUtilities.DpiYFactor;
		}
	}
}
