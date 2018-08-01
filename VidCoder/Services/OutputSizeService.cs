using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VidCoder.ViewModel;
using VidCoderCommon.Model;

namespace VidCoder.Services
{
	public class OutputSizeService : ReactiveObject
	{
		private PresetsService presetsService = Ioc.Get<PresetsService>();

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
			MainViewModel mainViewModel = Ioc.Get<MainViewModel>();

			if (mainViewModel.SelectedTitle != null)
			{
				var profile = this.presetsService.SelectedPreset.Preset.EncodingProfile;

				OutputSizeInfo outputSizeInfo = JsonEncodeFactory.GetOutputSize(profile, mainViewModel.SelectedTitle);

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
