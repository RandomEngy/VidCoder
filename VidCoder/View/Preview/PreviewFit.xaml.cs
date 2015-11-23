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
	public partial class PreviewFit : UserControl, IPreviewHolder
	{
		public PreviewFit()
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

		public void ResizeHolder(Grid previewArea, double widthPixels, double heightPixels, bool showOneToOneWhenSmaller)
		{
			ImageUtilities.UpdatePreviewHolderSize(this.previewHolder, previewArea, widthPixels, heightPixels, showOneToOneWhenSmaller);
		}
	}
}
