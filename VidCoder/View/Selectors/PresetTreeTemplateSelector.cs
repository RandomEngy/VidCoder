using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VidCoder.ViewModel.DataModels;

namespace VidCoder.View.Selectors
{
	public class PresetTreeTemplateSelector : DataTemplateSelector
	{
		public DataTemplate FolderTemplate { get; set; }
		public DataTemplate PresetTemplate { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (item is PresetFolderViewModel)
			{
				return this.FolderTemplate;
			}

			return this.PresetTemplate;
		}
	}
}
