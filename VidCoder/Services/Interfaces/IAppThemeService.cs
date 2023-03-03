using System;

namespace VidCoder.Services;

public interface IAppThemeService : IDisposable
{
	//event EventHandler<EventArgs<WindowsTheme>> ThemeChanged;
	//WindowsTheme CurrentTheme { get; }
	IObservable<AppTheme> AppThemeObservable { get; }
}