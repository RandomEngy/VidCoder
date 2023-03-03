﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop.Interop.Json.Scan;
using ReactiveUI;
using VidCoder.Extensions;

namespace VidCoder.ViewModel;

public class TitleSelectionViewModel : ReactiveObject
{
	private QueueTitlesWindowViewModel titlesDialogVM;
	private bool selected;

	public TitleSelectionViewModel(SourceTitle title, QueueTitlesWindowViewModel titlesDialogVM)
	{
		this.Title = title;
		this.titlesDialogVM = titlesDialogVM;
	}

	public bool Selected
	{
		get
		{
			return this.selected;
		}

		set
		{
			this.selected = value;
			this.RaisePropertyChanged();
			this.titlesDialogVM.HandleCheckChanged(this, value);
		}
	}

	public SourceTitle Title { get; set; }

	public string Text
	{
		get
		{
			return this.Title.GetDisplayString();
		}
	}

	/// <summary>
	/// Set the selected value for this item.
	/// </summary>
	/// <param name="newValue"></param>
	public void SetSelected(bool newValue)
	{
		this.selected = newValue;
		this.RaisePropertyChanged(nameof(this.Selected));
	}

	public override string ToString()
	{
		return this.Text;
	}
}
