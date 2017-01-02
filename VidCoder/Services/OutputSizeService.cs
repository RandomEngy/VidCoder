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

		private OutputSizeInfo size;
		public OutputSizeInfo Size
		{
			get { return this.size; }
			set { this.RaiseAndSetIfChanged(ref this.size, value); }
		}

		public void Refresh()
		{
			if (this.mainViewModel.SelectedTitle != null)
			{
				var profile = this.presetsService.SelectedPreset.Preset.EncodingProfile;

				OutputSizeInfo outputSizeInfo = JsonEncodeFactory.GetOutputSize(profile, this.mainViewModel.SelectedTitle);

				if (this.Size == null || !outputSizeInfo.Equals(this.Size))
				{
					this.Size = outputSizeInfo;
				}
			}
			else
			{
				this.Size = null;
			}
		}
	}
}
