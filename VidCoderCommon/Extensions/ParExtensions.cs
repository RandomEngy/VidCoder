using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HandBrake.Interop.Interop.Json.Shared;
using VidCoderCommon.Utilities;

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

			int gcd = MathUtilities.Gcd(par.Num, par.Den);
			par.Num /= gcd;
			par.Den /= gcd;
		}
	}
}
