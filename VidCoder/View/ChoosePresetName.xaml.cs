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

namespace VidCoder.View
{
    /// <summary>
    /// Interaction logic for ChoosePresetName.xaml
    /// </summary>
    public partial class ChoosePresetName : Window
    {
        public ChoosePresetName()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.presetNameText.Focus();
            this.presetNameText.SelectAll();
        }
    }
}
