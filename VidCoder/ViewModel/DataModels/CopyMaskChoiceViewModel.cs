using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;

namespace VidCoder.ViewModel.DataModels;

public class CopyMaskChoiceViewModel : ReactiveObject
{
	private static ResourceManager resourceManager = new(typeof(EncodingRes));

	public CopyMaskChoiceViewModel(string codec, bool enabled)
	{
		this.Codec = codec;
		this.enabled = enabled;
	}

	/// <summary>
	/// The codec.
	/// </summary>
	/// <remarks>This is the short encoder name without the "copy:" prefix.</remarks>
	public string Codec { get; private set; }

	private bool enabled;
	public bool Enabled
	{
		get { return this.enabled; }
		set { this.RaiseAndSetIfChanged(ref this.enabled, value); }
	}

	public string Display
	{
		get
		{
			string display = resourceManager.GetString("Passthrough_" + this.Codec);
			if (!string.IsNullOrEmpty(display))
			{
				return display;
			}

			return HandBrakeEncoderHelpers.GetAudioEncoder("copy:" + this.Codec).DisplayName;
		}
	}
}
