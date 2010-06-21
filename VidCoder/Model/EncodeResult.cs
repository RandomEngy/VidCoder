using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model
{
    public class EncodeResult
    {
        public string Destination { get; set; }
        public bool Succeeded { get; set; }
        public TimeSpan EncodeTime { get; set; }
    }
}
