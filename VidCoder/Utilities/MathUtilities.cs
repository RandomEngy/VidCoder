namespace VidCoder
{
	public static class MathUtilities
	{
		/// <summary>
		/// Gets the closest value to the given number divisible by the given modulus.
		/// </summary>
		/// <param name="number">The number to approximate.</param>
		/// <param name="modulus">The modulus.</param>
		/// <returns>The closest value to the given number divisible by the given modulus.</returns>
		public static int GetNearestValue(int number, int modulus)
		{
			return modulus * ((number + modulus / 2) / modulus);
		}
	}
}
