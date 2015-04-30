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
	using Model;

	/// <summary>
	/// Base class for view models of panels on the encoding settings window.
	/// </summary>
	public abstract class PanelViewModel : ViewModelBase
	{
		private EncodingViewModel encodingViewModel;

		protected PanelViewModel(EncodingViewModel encodingViewModel)
		{
			this.encodingViewModel = encodingViewModel;
		}

		public VCProfile Profile
		{
			get
			{
				return this.encodingViewModel.EncodingProfile;
			}
		}

		public EncodingViewModel EncodingViewModel
		{
			get
			{
				return this.encodingViewModel;
			}
		}

		public bool AutomaticChange
		{
			get
			{
				return this.encodingViewModel.AutomaticChange;
			}

			set
			{
				this.encodingViewModel.AutomaticChange = value;
			}
		}

		public bool IsModified
		{
			get
			{
				return this.encodingViewModel.IsModified;
			}

			set
			{
				this.encodingViewModel.IsModified = value;
			}
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.encodingViewModel.MainViewModel;
			}
		}

		public bool HasSourceData
		{
			get
			{
				return this.encodingViewModel.MainViewModel.HasVideoSource;
			}
		}

		public SourceTitle SelectedTitle
		{
			get
			{
				return this.encodingViewModel.MainViewModel.SelectedTitle;
			}
		}

		public void UpdatePreviewWindow()
		{
			Messenger.Default.Send(new RefreshPreviewMessage());
		}

		public virtual void NotifySelectedTitleChanged()
		{
			this.RaisePropertyChanged(() => this.HasSourceData);
		}
	}
}
