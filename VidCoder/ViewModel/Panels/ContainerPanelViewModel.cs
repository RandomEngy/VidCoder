using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using HandBrake.Interop.Interop.Model.Encoding;
using Microsoft.AnyContainer;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel.Panels
{
	public class ContainerPanelViewModel : PanelViewModel
	{
		private readonly List<ComboChoice> containerChoices;

		public ContainerPanelViewModel(EncodingWindowViewModel encodingWindowViewModel) 
			: base(encodingWindowViewModel)
		{
			this.AutomaticChange = true;

			this.RegisterProfileProperties();

			this.containerChoices = new List<ComboChoice>();
			foreach (HBContainer hbContainer in HandBrakeEncoderHelpers.Containers)
			{
				this.containerChoices.Add(new ComboChoice(hbContainer.ShortName, hbContainer.DefaultExtension.ToUpperInvariant()));
			}

			this.WhenAnyValue(
					x => x.ContainerName,
					containerName =>
					{
						HBContainer container = HandBrakeEncoderHelpers.GetContainer(containerName);
						return container.DefaultExtension == "mp4";
					})
				.ToProperty(this, x => x.ShowMp4Choices, out this.showMp4Choices);

			this.AutomaticChange = false;
		}

		private void RegisterProfileProperties()
		{
			this.RegisterProfileProperty(nameof(this.Profile.ContainerName), () =>
			{
				this.OutputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(nameof(this.Profile.PreferredExtension), () =>
			{
				this.OutputPathService.GenerateOutputFileName();
			});

			this.RegisterProfileProperty(nameof(this.Profile.Optimize));
			this.RegisterProfileProperty(nameof(this.Profile.IPod5GSupport));
			this.RegisterProfileProperty(nameof(this.Profile.AlignAVStart));

			this.RegisterProfileProperty(nameof(this.IncludeChapterMarkers));
		}

		public OutputPathService OutputPathService { get; } = StaticResolver.Resolve<OutputPathService>();

		public string ContainerName
		{
			get { return this.Profile.ContainerName; }
			set { this.UpdateProfileProperty(nameof(this.Profile.ContainerName), value); }
		}

		public List<ComboChoice> ContainerChoices => this.containerChoices;

		public VCOutputExtension PreferredExtension
		{
			get { return this.Profile.PreferredExtension; }
			set { this.UpdateProfileProperty(nameof(this.Profile.PreferredExtension), value); }
		}

		public bool Optimize
		{
			get { return this.Profile.Optimize; }
			set { this.UpdateProfileProperty(nameof(this.Profile.Optimize), value); }
		}

		public bool AlignAVStart
		{
			get { return this.Profile.AlignAVStart; }
			set { this.UpdateProfileProperty(nameof(this.Profile.AlignAVStart), value); }
		}

		public bool IPod5GSupport
		{
			get { return this.Profile.IPod5GSupport; }
			set { this.UpdateProfileProperty(nameof(this.Profile.IPod5GSupport), value); }
		}

		private ObservableAsPropertyHelper<bool> showMp4Choices;
		public bool ShowMp4Choices => this.showMp4Choices.Value;

		public bool IncludeChapterMarkers
		{
			get { return this.Profile.IncludeChapterMarkers; }
			set { this.UpdateProfileProperty(nameof(this.Profile.IncludeChapterMarkers), value); }
		}
	}
}
