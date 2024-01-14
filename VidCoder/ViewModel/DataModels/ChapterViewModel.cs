using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Interop.Json.Scan;
using ReactiveUI;

namespace VidCoder.ViewModel;

public class ChapterViewModel : ReactiveObject
{
	/// <summary>
	/// Initializes a new instance of the ChapterViewModel class.
	/// </summary>
	/// <param name="chapter">The raw source chapter.</param>
	/// <param name="chapterNumber">The 1-based index of the chapter.</param>
	public ChapterViewModel(SourceChapter chapter, int chapterNumber)
	{
		this.Chapter = chapter;
		this.ChapterNumber = chapterNumber;
	}

	public SourceChapter Chapter { get; private set; }

	public int ChapterNumber { get; private set; }

	private bool isHighlighted;
	public bool IsHighlighted
	{
		get { return this.isHighlighted; }
		set { this.RaiseAndSetIfChanged(ref this.isHighlighted, value); }
	}

	public override string ToString()
	{
		return this.ChapterNumber.ToString();
	}
}
