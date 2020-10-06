using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using VidCoder.Extensions;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for OptionsDialog.xaml
	/// </summary>
	public partial class OptionsDialog : Window
	{
		public OptionsDialog()
		{
			this.InitializeComponent();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			this.DataContext = null;
		}

		private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
			e.Handled = true;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			// Works around a Logitech mouse driver bug, code from https://developercommunity.visualstudio.com/content/problem/167357/overflow-exception-in-windowchrome.html
			((HwndSource)PresentationSource.FromVisual(this)).AddHook(WindowUtilities.HookProc);
		}
	}
}
