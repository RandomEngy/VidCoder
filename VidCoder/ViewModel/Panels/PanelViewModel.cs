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
		private EncodingWindowViewModel encodingWindowViewModel;

		protected PanelViewModel(EncodingWindowViewModel encodingWindowViewModel)
		{
			this.encodingWindowViewModel = encodingWindowViewModel;
		}

		public VCProfile Profile
		{
			get
			{
				return this.encodingWindowViewModel.EncodingProfile;
			}
		}

		public EncodingWindowViewModel EncodingWindowViewModel
		{
			get
			{
				return this.encodingWindowViewModel;
			}
		}

		public bool AutomaticChange
		{
			get
			{
				return this.encodingWindowViewModel.AutomaticChange;
			}

			set
			{
				this.encodingWindowViewModel.AutomaticChange = value;
			}
		}

		public bool IsModified
		{
			get
			{
				return this.encodingWindowViewModel.IsModified;
			}

			set
			{
				this.encodingWindowViewModel.IsModified = value;
			}
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.encodingWindowViewModel.MainViewModel;
			}
		}

		public bool HasSourceData
		{
			get
			{
				return this.encodingWindowViewModel.MainViewModel.HasVideoSource;
			}
		}

		public SourceTitle SelectedTitle
		{
			get
			{
				return this.encodingWindowViewModel.MainViewModel.SelectedTitle;
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
