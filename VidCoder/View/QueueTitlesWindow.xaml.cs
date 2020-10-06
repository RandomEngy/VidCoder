using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Extensions;
using VidCoder.Services;
using VidCoder.ViewModel;

namespace VidCoder.View
{
	/// <summary>
	/// Interaction logic for MultipleTitlesDialog.xaml
	/// </summary>
	public partial class QueueTitlesWindow : Window
	{
		private QueueTitlesWindowViewModel viewModel;

		public QueueTitlesWindow()
		{
			InitializeComponent();

			this.DataContextChanged += this.OnDataContextChanged;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			// Works around a Logitech mouse driver bug, code from https://developercommunity.visualstudio.com/content/problem/167357/overflow-exception-in-windowchrome.html
			((HwndSource)PresentationSource.FromVisual(this)).AddHook(WindowUtilities.HookProc);
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.viewModel = this.DataContext as QueueTitlesWindowViewModel;
			this.viewModel.PropertyChanged += this.viewModel_PropertyChanged;
		}

		private void viewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(this.viewModel.PreviewImage))
			{
				this.RefreshImageSize();
			}
		}

		private void RefreshImageSize()
		{
			var previewVM = this.DataContext as QueueTitlesWindowViewModel;
			if (previewVM.SelectedTitles.Count != 1)
			{
				return;
			}

			double widthPixels, heightPixels;

			SourceTitle title = previewVM.SelectedTitles[0].Title;
			if (title.Geometry.PAR.Num > title.Geometry.PAR.Den)
			{
				widthPixels = title.Geometry.Width * ((double)title.Geometry.PAR.Num / title.Geometry.PAR.Den);
				heightPixels = title.Geometry.Height;
			}
			else
			{
				widthPixels = title.Geometry.Width;
				heightPixels = title.Geometry.Height * ((double)title.Geometry.PAR.Den / title.Geometry.PAR.Num);
			}

			ImageUtilities.UpdatePreviewHolderSize(this.previewImage, this.previewImageHolder, widthPixels, heightPixels, showOneToOneWhenSmaller: true);
		}

		private void previewImageHolder_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			this.RefreshImageSize();
		}
	}
}
