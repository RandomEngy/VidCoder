using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for QueueColumnsDialog.xaml
	/// </summary>
	public partial class QueueColumnsDialog : Window
	{
		public QueueColumnsDialog()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			// Works around a Logitech mouse driver bug, code from https://developercommunity.visualstudio.com/content/problem/167357/overflow-exception-in-windowchrome.html
			((HwndSource)PresentationSource.FromVisual(this)).AddHook(WindowUtilities.HookProc);
		}
	}
}
