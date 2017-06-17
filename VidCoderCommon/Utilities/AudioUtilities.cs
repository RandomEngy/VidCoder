using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.ApplicationServices.Interop;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using VidCoderCommon.Model;

namespace VidCoderCommon
{
    public static class AudioUtilities
    {
        public static OutputAudioTrackInfo GetDefaultSettings(SourceAudioTrack source, HBAudioEncoder encoder)
        {
            var result = new OutputAudioTrackInfo
            {   
                Encoder = encoder,
                Drc = 0,
                Gain = 0,
                SampleRate = HandBrakeEncoderHelpers.SanitizeSampleRate(encoder, source.SampleRate)
            };

            if (encoder.SupportsCompression)
            {
                result.CompressionLevel = HandBrakeEncoderHelpers.GetDefaultAudioCompression(encoder);
            }

            HBMixdown mixdown = HandBrakeEncoderHelpers.GetDefaultMixdown(encoder, (ulong)source.ChannelLayout);
            result.Mixdown = mixdown;

            // For some reason HB does not honor Quality when falling back to an encoder during auto-passthrough.
            // For now we do bitrate only.

            //if (encoder.SupportsQuality)
            //{
            //    result.EncodeRateType = AudioEncodeRateType.Quality;
            //    result.Quality = HandBrakeEncoderHelpers.GetDefaultQuality(encoder);
            //}
            //else
            //{

            result.EncodeRateType = AudioEncodeRateType.Bitrate;
            result.Bitrate = HandBrakeEncoderHelpers.GetDefaultBitrate(encoder, result.SampleRate, mixdown);

            //}

            return result;
        }
    }
}
