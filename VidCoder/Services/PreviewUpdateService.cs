using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services
{
	public class PreviewUpdateService
	{
		public event EventHandler PreviewInputChanged;

		public void RefreshPreview()
		{
			this.PreviewInputChanged?.Invoke(this, new EventArgs());
		}
	}
}
