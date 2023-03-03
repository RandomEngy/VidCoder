using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Model;

public class ComboChoice<T>
{
	public ComboChoice(T value, string display)
	{
		this.Value = value;
		this.Display = display;
	}

	public T Value { get; set; }
	public string Display { get; set; }

    public override string ToString()
    {
        return this.Display;
    }
}
