using System;
using System.Windows;

namespace VidCoder.Services.Windows;

public class WindowDefinition
{
	public Type ViewModelType { get; set; }

	public string PlacementConfigKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the window will manually handle the placement restore on opening.
	/// </summary>
	public bool ManualPlacementRestore { get; set; }

	public string IsOpenConfigKey { get; set; }

	public bool InMenu { get; set; }

	public string MenuLabel { get; set; }

	public string InputGestureText { get; set; }

	public Func<IObservable<bool>> CanOpen { get; set; } 

	public Func<Size> InitialSizeOverride { get; set; }
}
