using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.View.Selectors;

public class PresetTreeContainerStyleSelector : StyleSelector
{
	public Style FolderStyle { get; set; }
	public Style PresetStyle { get; set; }

	public override Style SelectStyle(object item, DependencyObject container)
	{
		if (item is PresetFolderViewModel)
		{
			return this.FolderStyle;
		}

		return this.PresetStyle;
	}
}
