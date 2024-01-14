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
using System.Windows.Shapes;

namespace VidCoder.View;

/// <summary>
/// Interaction logic for WatcherWindow.xaml
/// </summary>
public partial class WatcherWindow : Window
{
	public WatcherWindow()
	{
		this.InitializeComponent();

		this.LoadFileColumnWidths();

		this.Closing += OnClosing;
	}

	private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
	{
		this.SaveFileColumnWidths();
	}

	public void LoadFileColumnWidths()
	{
		string columnWidthsString = Config.WatcherFileColumnWidths;

		if (string.IsNullOrEmpty(columnWidthsString))
		{
			return;
		}

		string[] columnWidths = columnWidthsString.Split('|');
		for (int i = 0; i < this.filesGridView.Columns.Count; i++)
		{
			if (i < columnWidths.Length)
			{
				double width = 0;
				double.TryParse(columnWidths[i], out width);

				if (width > 0)
				{
					this.filesGridView.Columns[i].Width = width;
				}
			}
		}
	}

	private void SaveFileColumnWidths()
	{
		var columnsBuilder = new StringBuilder();
		for (int i = 0; i < this.filesGridView.Columns.Count; i++)
		{
			columnsBuilder.Append(this.filesGridView.Columns[i].ActualWidth);

			if (i != this.filesGridView.Columns.Count - 1)
			{
				columnsBuilder.Append("|");
			}
		}

		Config.WatcherFileColumnWidths = columnsBuilder.ToString();
	}
}
