using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Json.Shared;

namespace VidCoderCommon.Utilities
{
	public class MathUtilities
	{
		public static long Gcd(long a, long b)
		{
			if (a < 0 || b < 0)
			{
				return 1;
			}

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

		public static int Gcd(int a, int b)
		{
			if (a < 0 || b < 0)
			{
				return 1;
			}

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

		public static PAR CreatePar(long num, long den)
		{
			if (num == 0 || den == 0)
			{
				return new PAR { Num = (int)num, Den = (int)den };
			}

			long gcd = Gcd(num, den);
			return new PAR { Num = (int)(num / gcd), Den = (int)(den / gcd) };
		}

		public static int RoundToModulus(int number, int modulus)
		{
			return (int)Math.Round((double)number / modulus) * modulus;
		}
	}
}
