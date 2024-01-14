using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VidCoder.Resources;

namespace VidCoder.ViewModel.DataModels;

public class TrackNameViewModel : ReactiveObject
{
	private readonly PickerWindowViewModel pickerViewModel;

	public TrackNameViewModel(PickerWindowViewModel pickerViewModel, int initTrackNumber, string initName)
	{
		this.trackNumber = initTrackNumber;
		this.name = initName;

		// TrackNumberDisplay
		this.WhenAnyValue(x => x.TrackNumber)
			.Select(trackNumber =>
			{
				return trackNumber + ":";
			}).ToProperty(this, x => x.TrackNumberDisplay, out this.trackNumberDisplay);

		// AutomationName
		this.WhenAnyValue(x => x.TrackNumber)
			.Select(trackNumber =>
			{
				return string.Format(PickerRes.TrackAutomationFormat, trackNumber);
			}).ToProperty(this, x => x.AutomationName, out this.automationName);
		this.pickerViewModel = pickerViewModel;
	}

	private int trackNumber;

	/// <summary>
	/// The 1-based track number this name should apply to.
	/// </summary>
	public int TrackNumber
	{
		get => this.trackNumber;
		set => this.RaiseAndSetIfChanged(ref this.trackNumber, value);
	}

	private string name;

	/// <summary>
	/// The name for the track.
	/// </summary>
	public string Name
	{
		get => this.name;
		set
		{
			this.RaiseAndSetIfChanged(ref this.name, value);
			this.pickerViewModel.HandleTrackNameUpdate(this);
		}
	}

	private ObservableAsPropertyHelper<string> trackNumberDisplay;
	public string TrackNumberDisplay => this.trackNumberDisplay.Value;

	private ObservableAsPropertyHelper<string> automationName;
	public string AutomationName => this.automationName.Value;

	private ReactiveCommand<Unit, Unit> remove;

	public ICommand Remove
	{
		get
		{
			return this.remove ?? (this.remove = ReactiveCommand.Create(() =>
			{
				pickerViewModel.RemoveTrackName(this);
			}));
		}
	}
}
