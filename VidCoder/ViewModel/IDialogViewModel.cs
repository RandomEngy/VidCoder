using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace VidCoder.ViewModel
{
	public interface IDialogViewModel
	{
		bool CanClose { get; }
		void OnClosing();
	}
}
