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

namespace VidCoder.Controls
{
	/// <summary>
	/// Interaction logic for PickerList.xaml
	/// </summary>
	public partial class PickerList : UserControl
	{
		public PickerList()
		{
			this.InitializeComponent();
		}

		public event MouseEventHandler ItemMouseUp;

		private void OnItemMouseUp(object sender, MouseButtonEventArgs e)
		{
			this.ItemMouseUp?.Invoke(this, e);
		}
	}
}
