using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.ViewModel;

public class BitrateChoiceViewModel
{
	public int Bitrate { get; set; }

	public bool IsCompatible { get; set; }

    public override string ToString()
    {
            if (this.Bitrate > 0)
            {
                return this.Bitrate.ToString();
            }
            else
            {
                return "Auto";
            }
        }
}
