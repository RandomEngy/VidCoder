using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using VidCoder.Extensions;
using VidCoder.Services;
using VidCoder.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace VidCoder.View
{
	using System.Data.SQLite;
	using Model;

	/// <summary>
	/// Interaction logic for EncodingWindow.xaml
	/// </summary>
	public partial class EncodingWindow : Window
	{
		public EncodingWindow()
		{
			InitializeComponent();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.PlaceDynamic(Config.EncodingDialogPlacement);
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.EncodingDialogPlacement = this.GetPlacementXml();
				Config.EncodingDialogLastTab = this.tabControl.SelectedIndex;

				transaction.Commit();
			}
		}

		private void ToolBar_Loaded(object sender, RoutedEventArgs e)
		{
			UIUtilities.HideOverflowGrid(sender as ToolBar);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			this.DataContext = null;
		}
	}
}
