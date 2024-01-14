using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Interfaces.Model.Encoders;

namespace VidCoderCommon.Model;

    /// <summary>
    /// Intermediate type for holding information about a non-passthrough output audio track.
    /// </summary>
    public class OutputAudioTrackInfo
    {
        public HBAudioEncoder Encoder { get; set; }

        public int SampleRate { get; set; }

        public HBMixdown Mixdown { get; set; }

        public double? CompressionLevel { get; set; }

        public AudioEncodeRateType EncodeRateType { get; set; }

        public int Bitrate { get; set; }

        public double Quality { get; set; }

        public double Drc { get; set; }

        public double Gain { get; set; }
    }
