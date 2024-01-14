using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model;

public class ComboChoice
{
	public ComboChoice(string value)
	{
		this.Value = value;
		this.Display = value;
	}

	public ComboChoice(string value, string display)
	{
		this.Value = value;
		this.Display = display;
	}

	public string Value { get; set; }
	public string Display { get; set; }

        public override string ToString()
        {
            return this.Display;
        }
    }
