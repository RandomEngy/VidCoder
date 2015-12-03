using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.ApplicationServices.Interop.Json.Shared;

namespace VidCoderCommon.Extensions
{
	public static class ParExtensions
	{
		public static void Simplify(this PAR par)
		{
			if (par.Num == 0 || par.Den == 0)
			{
				return;
			}

			int gcd = Gcd(par.Num, par.Den);
			par.Num /= gcd;
			par.Den /= gcd;
		}

		private static int Gcd(int a, int b)
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
	}
}
