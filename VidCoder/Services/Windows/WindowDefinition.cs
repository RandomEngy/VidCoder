using System;
using System.Linq.Expressions;
using System.Windows.Input;

namespace VidCoder.Services.Windows
{
	public class WindowDefinition
	{
		public Type ViewModelType { get; set; }

		public string PlacementConfigKey { get; set; }

		public string IsOpenConfigKey { get; set; }

		public bool InMenu { get; set; }

		public string MenuLabel { get; set; }

		public string InputGestureText { get; set; }
	}
}
