using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.ViewModel.DataModels
{
	public class InfoLineViewModel
	{
		public InfoLineViewModel(string label, string value)
		{
			this.Label = label;
			this.Value = value;
		}

		public string Label { get; }

		public string Value { get; }
	}
}
