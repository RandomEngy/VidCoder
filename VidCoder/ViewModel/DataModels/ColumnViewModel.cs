using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using GalaSoft.MvvmLight;
using VidCoder.Properties;

namespace VidCoder.ViewModel
{
	public class ColumnViewModel : ViewModelBase
	{
		private static ResourceManager resources = new ResourceManager("VidCoder.Properties.Resources", typeof(Resources).Assembly);

		public ColumnViewModel(string id)
		{
			this.Id = id;
		}

		public string Id { get; set; }

		public string Title
		{
			get
			{
				return resources.GetString("QueueColumnName" + this.Id);
			}
		}
	}
}
