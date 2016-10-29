using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCoderCommon.Utilities
{
	public class MathUtilities
	{
		public static int Gcd(int a, int b)
		{
			while (a != 0 && b != 0)
			{
				if (a > b)
				{
					a %= b;
				}
				else
				{
					b %= a;
				}
			}

			if (a == 0)
			{
				return b;
			}
			else
			{
				return a;
			}
		}

		public static int RoundToModulus(int number, int modulus)
		{
			return (int)Math.Round((double)number / modulus) * modulus;
		}
	}
}
