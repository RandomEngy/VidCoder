using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel
{
	public class InterfaceLanguage
	{
		public string CultureCode { get; set; }

		public string Display { get; set; }

	    public override string ToString()
	    {
	        return this.Display;
	    }
	}
}
