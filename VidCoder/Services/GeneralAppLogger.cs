using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoder.Services;

    public class GeneralAppLogger : AppLogger
    {
    public GeneralAppLogger(IAppLogger parent) 
		: base(parent, "General")
    {
    }
    }
