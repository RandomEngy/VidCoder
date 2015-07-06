using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
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
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.View
{
    /// <summary>
    /// Interaction logic for PickerWindow.xaml
    /// </summary>
    public partial class PickerWindow : Window
    {
        public PickerWindow()
        {
            this.InitializeComponent();
			this.listColumn.Width = new GridLength(Config.PickerListPaneWidth);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
	        this.PlaceDynamic(Config.PickerWindowPlacement);
        }

        private void PickerWindow_OnClosing(object sender, CancelEventArgs e)
        {
	        using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
	        {
		        Config.PickerWindowPlacement = this.GetPlacementJson();
		        Config.PickerListPaneWidth = this.listColumn.ActualWidth;

				transaction.Commit();
			}
        }

        private void ToolBar_Loaded(object sender, RoutedEventArgs e)
        {
            UIUtilities.HideOverflowGrid(sender as ToolBar);
        }
    }
}
