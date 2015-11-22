using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.ApplicationServices.Interop.Json.Shared;
using ReactiveUI;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	public class OutputSizeService : ReactiveObject
	{
		private PresetsService presetsService = Ioc.Get<PresetsService>();
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();

		public OutputSizeService()
		{
			this.Refresh();

			this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.Refresh();
				});
		}

		private Geometry size;
		public Geometry Size
		{
			get { return this.size; }
			set { this.RaiseAndSetIfChanged(ref this.size, value); }
		}

		public void Refresh()
		{
			if (this.mainViewModel.SelectedTitle != null)
			{
				var profile = this.presetsService.SelectedPreset.Preset.EncodingProfile;

				Geometry outputGeometry = JsonEncodeFactory.GetAnamorphicSize(profile, this.mainViewModel.SelectedTitle);

				int width = outputGeometry.Width;
				int height = outputGeometry.Height;
				int parWidth = outputGeometry.PAR.Num;
				int parHeight = outputGeometry.PAR.Den;

				if (profile.Rotation == VCPictureRotation.Clockwise90 || profile.Rotation == VCPictureRotation.Clockwise270)
				{
					int temp = width;
					width = height;
					height = temp;

					temp = parWidth;
					parWidth = parHeight;
					parHeight = temp;
				}

				if (this.Size == null ||
				    width != this.Size.Width ||
				    height != this.Size.Height ||
				    parWidth != this.Size.PAR.Num ||
				    parHeight != this.Size.PAR.Den)
				{
					this.Size = new Geometry
					{
						Width = width,
						Height = height,
						PAR = new PAR
						{
							Num = parWidth,
							Den = parHeight
						}
					};
				}
			}
			else
			{
				this.Size = null;
			}
		}
	}
}
