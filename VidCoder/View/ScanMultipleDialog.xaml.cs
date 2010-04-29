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
using VidCoder.ViewModel;

namespace VidCoder.View
{
    /// <summary>
    /// Interaction logic for ScanMultipleDialog.xaml
    /// </summary>
    public partial class ScanMultipleDialog : Window
    {
        public ScanMultipleDialog()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ScanMultipleDialogViewModel viewModel = this.DataContext as ScanMultipleDialogViewModel;
            if (!viewModel.CanClose)
            {
                e.Cancel = true;
            }
        }
    }
}
