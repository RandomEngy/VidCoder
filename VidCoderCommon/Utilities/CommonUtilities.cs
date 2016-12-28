using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon
{
    public static class CommonUtilities
    {
        public static bool Beta
        {
            get
            {
#if BETA
				return true;
#else
                return false;
#endif
            }
        }

        public static bool DebugMode
        {
            get
            {
#if DEBUG
				return true;
#else
                return false;
#endif
            }
        }
    }
}
