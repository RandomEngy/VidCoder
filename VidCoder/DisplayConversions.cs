using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.Interop;

namespace VidCoder
{
    public static class DisplayConversions
    {
        public static string DisplayAudioEncoder(AudioEncoder audioEncoder)
        {
            switch (audioEncoder)
            {
                case AudioEncoder.Ac3Passthrough:
                    return "AC3 Passthrough";
                case AudioEncoder.DtsPassthrough:
                    return "DTS Passthrough";
                case AudioEncoder.Faac:
                    return "AAC (faac)";
                case AudioEncoder.Lame:
                    return "MP3 (lame)";
                case AudioEncoder.Vorbis:
                    return "Vorbis (vorbis)";
                default:
                    return string.Empty;
            }
        }

        public static string DisplayMixdown(Mixdown mixdown)
        {
            switch (mixdown)
            {
                case Mixdown.DolbyProLogicII:
                    return "Dolby Pro Logic II";
                case Mixdown.DolbySurround:
                    return "Dolby Surround";
                case Mixdown.Mono:
                    return "Mono";
                case Mixdown.SixChannelDiscrete:
                    return "6 Channel Discrete";
                case Mixdown.Stereo:
                    return "Stereo";
                default:
                    return string.Empty;
            }
        }
    }
}
