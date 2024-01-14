using ReactiveUI;

namespace VidCoder.ViewModel;

public class ChapterNameViewModel : ReactiveObject
{
	private int number;
	public int Number
	{
		get { return this.number; }
		set { this.RaiseAndSetIfChanged(ref this.number, value); }
	}

	private string title;
	public string Title
	{
		get { return this.title; }
		set { this.RaiseAndSetIfChanged(ref this.title, value); }
	}

	private string startTime;
	public string StartTime
	{
		get { return this.startTime; }
		set { this.RaiseAndSetIfChanged(ref this.startTime, value); }
	}
}
