using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Messages;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	/// <summary>
	/// Base class for view models of panels on the encoding settings window.
	/// </summary>
	public abstract class PanelViewModel : ProfileViewModelBase
	{
		private EncodingWindowViewModel encodingWindowViewModel;

		protected PanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
		{
			this.encodingWindowViewModel = encodingWindowViewModel;
		}

		public EncodingWindowViewModel EncodingWindowViewModel
		{
			get
			{
				return this.encodingWindowViewModel;
			}
		}

		public SourceTitle SelectedTitle
		{
			get
			{
				return this.encodingWindowViewModel.MainViewModel.SelectedTitle;
			}
		}
	}
}
