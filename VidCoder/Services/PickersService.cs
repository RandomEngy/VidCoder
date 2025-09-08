using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.AnyContainer;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services.Windows;
using VidCoder.ViewModel;
using VidCoder.ViewModel.DataModels;
using VidCoderCommon.Utilities.Injection;

namespace VidCoder.Services;

/// <summary>
/// Controls creation/modification/deletion/import/export of pickers.
/// </summary>
public class PickersService : ReactiveObject
{
	private MainViewModel main = StaticResolver.Resolve<MainViewModel>();
	private IWindowManager windowManager = StaticResolver.Resolve<IWindowManager>();
	private OutputPathService outputPathService = StaticResolver.Resolve<OutputPathService>();

	private PickerViewModel selectedPicker;

	private bool executingNonDragAdd;

	private ObservableCollection<PickerViewModel> pickers;

	public PickersService()
	{
		this.pickers = new ObservableCollection<PickerViewModel>();
		List<Picker> storedPickers = PickerStorage.PickerList;
		var unmodifiedPickers = storedPickers.Where(picker => !picker.IsModified);
		Picker modifiedPicker = storedPickers.FirstOrDefault(picker => picker.IsModified);
		int modifiedPickerIndex = -1;

		Picker nonePicker = new()
		{
			IsDefault = true,
			Name = string.Empty
		};
		this.pickers.Add(new PickerViewModel(nonePicker));

		this.WhenAnyValue(x => x.SelectedPicker.DisplayNameWithStar)
			.Select(displayName =>
			{
				return string.Format(PickerRes.PickerButtonFormat, displayName);
			})
			.ToProperty(this, x => x.PickerButtonText, out this.pickerButtonText, deferSubscription: true);

		this.WhenAnyValue(x => x.SelectedPicker.Picker.UseEncodingPreset)
			.Select(useEncodingPreset =>
			{
				return useEncodingPreset ? PickerRes.PresetDisabledForPickerToolTip : null;
			})
			.ToProperty(this, x => x.PresetDisabledToolTip, out this.presetDisabledToolTip);

		// When the picker preset changes, we need to update the selected preset.
		this.WhenAnyValue(x => x.SelectedPicker.Picker.EncodingPreset).Subscribe(pickerPreset =>
		{
			if (pickerPreset != null)
			{
				var presetsService = StaticResolver.Resolve<PresetsService>();
				PresetViewModel preset = presetsService.AllPresets.FirstOrDefault(p => p.Preset.Name == pickerPreset);
				if (preset == null)
				{
					preset = presetsService.AllPresets.First();
				}

				presetsService.SelectedPreset = preset;
			}
		});

		foreach (Picker storedPicker in unmodifiedPickers)
		{
			PickerViewModel pickerVM;
			if (modifiedPicker != null && modifiedPicker.Name == storedPicker.Name)
			{
				modifiedPickerIndex = this.pickers.Count;
				pickerVM = new PickerViewModel(modifiedPicker);
				pickerVM.OriginalPicker = storedPicker;
			}
			else
			{
				pickerVM = new PickerViewModel(storedPicker);
			}

			this.pickers.Add(pickerVM);
		}

		int pickerIndex;
		if (modifiedPickerIndex >= 0)
		{
			pickerIndex = modifiedPickerIndex;
		}
		else
		{
			pickerIndex = Config.LastPickerIndex;
		}

		if (pickerIndex >= this.pickers.Count)
		{
			pickerIndex = 0;
		}

		this.pickers.CollectionChanged += this.OnPickersChanged;

		this.selectedPicker = this.pickers[pickerIndex];
	}

	private void OnPickersChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		// Drag/drop fires this when it's re-added the dragged item. We need to persist this change.
		if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && !this.executingNonDragAdd)
		{
			this.SavePickersToStorage();
		}
	}

	public ObservableCollection<PickerViewModel> Pickers
	{
		get { return this.pickers; }
	}

	public PickerViewModel SelectedPicker
	{
		get { return this.selectedPicker; }

		set
		{
			if (value == null)
			{
				return;
			}

			bool changeSelectedPicker = true;

			if (this.selectedPicker != null && this.selectedPicker.Picker.IsModified)
			{
				MessageBoxResult dialogResult = Utilities.MessageBox.Show(
					this.main,
					MainRes.SaveChangesPickerMessage,
					MainRes.SaveChangesPickerTitle,
					MessageBoxButton.YesNoCancel);

				switch (dialogResult)
				{
					case MessageBoxResult.Yes:
						this.SavePicker();
						break;
					case MessageBoxResult.No:
						this.RevertPicker();
						break;
					case MessageBoxResult.Cancel:
						// Queue up action to switch back to this picker
						int currentPickerIndex = this.Pickers.IndexOf(this.selectedPicker);
						DispatchUtilities.BeginInvoke(() =>
						{
							this.SelectedPicker = this.Pickers[currentPickerIndex];
						});

						changeSelectedPicker = false;
						break;
				}
			}

			this.selectedPicker = value;
			if (changeSelectedPicker)
			{
				this.NotifySelectedPickerChanged();
			}
		}
	}

	public PickerViewModel GetPickerByName(string name)
	{
		return this.Pickers.FirstOrDefault(picker => picker.Picker.Name == name) ?? this.SelectedPicker;
	}

	private ObservableAsPropertyHelper<string> pickerButtonText;
	public string PickerButtonText => this.pickerButtonText.Value;

	private ObservableAsPropertyHelper<string> presetDisabledToolTip;
	public string PresetDisabledToolTip => this.presetDisabledToolTip.Value;

	public Collection<object> PickerButtonMenuItems
	{
		get
		{
			var result = new Collection<object>();

			foreach (var picker in this.Pickers)
			{
				var selectPickerCommand = this.CreateSelectPickerCommand(picker);

				result.Add(new MenuItem
				{
					Header = picker.DisplayNameWithStar,
					IsCheckable = true,
					IsChecked = picker == this.SelectedPicker,
					Command = selectPickerCommand,
					HorizontalContentAlignment = HorizontalAlignment.Left,
					VerticalContentAlignment = VerticalAlignment.Center,
				});
			}

			result.Add(new Separator());
			result.Add(
				new MenuItem
				{
					Header = MainRes.EditButton,
					Command = this.windowManager.CreateOpenCommand(typeof(PickerWindowViewModel)),
					HorizontalContentAlignment = HorizontalAlignment.Left,
					VerticalContentAlignment = VerticalAlignment.Center,
				});

			return result;
		}
	}

	public ReactiveCommand<Unit, Unit> CreateSelectPickerCommand(PickerViewModel picker)
	{
		var selectPickerCommand = ReactiveCommand.Create(() => 
		{
			if (picker != this.SelectedPicker)
			{
				this.SelectedPicker = picker;
			}
		});

		return selectPickerCommand;
	}

	public void SavePicker()
	{
		if (this.SelectedPicker.Picker.IsModified)
		{
			this.SelectedPicker.OriginalPicker = null;
			this.SelectedPicker.Picker.IsModified = false;
		}

		this.RefreshPickerButton();
		this.SavePickersToStorage();
	}

	public void SavePickerAs(string newName)
	{
		var newPicker = new Picker();
		newPicker.InjectFrom<CloneInjection>(this.SelectedPicker.Picker);
		newPicker.Name = newName;
		newPicker.IsModified = false;

		var newPickerVM = new PickerViewModel(newPicker);

		// Insert right after the current picker
		int insertionIndex = this.Pickers.IndexOf(this.SelectedPicker) + 1;
		this.InsertNewPicker(insertionIndex, newPickerVM);

		if (this.SelectedPicker.Picker.IsModified)
		{
			this.RevertPicker();
		}

		this.selectedPicker = null;
		this.SelectedPicker = newPickerVM;

		this.SavePickersToStorage();
	}

	public void RevertPicker()
	{
		Trace.Assert(this.SelectedPicker.OriginalPicker != null, "Error reverting preset: Original profile cannot be null.");
		Trace.Assert(this.SelectedPicker.OriginalPicker != this.SelectedPicker.Picker, "Error reverting preset: Original profile must be different from current profile.");

		if (this.SelectedPicker.OriginalPicker == null || this.SelectedPicker.OriginalPicker == this.SelectedPicker.Picker)
		{
			return;
		}

		this.SelectedPicker.Picker = this.SelectedPicker.OriginalPicker;
		this.SelectedPicker.OriginalPicker = null;
		this.SelectedPicker.Picker.IsModified = false;

		this.SavePickersToStorage();
	}

	public void DeletePicker()
	{
		this.Pickers.Remove(this.SelectedPicker);
		this.selectedPicker = null;
		this.SelectedPicker = this.Pickers[0];

		this.SavePickersToStorage();
	}

	public void AutoCreatePicker()
	{
		Picker newPicker = new();
		newPicker.Name = PickerStorage.CreateCustomPickerName(this.Pickers.Select(p => p.Picker).ToList());

		var newPickerVM = new PickerViewModel(newPicker);

		// Insert directly below "Default" so it's immediately visible
		this.InsertNewPicker(1, newPickerVM);

		this.selectedPicker = null;
		this.SelectedPicker = newPickerVM;
	}

	/// <summary>
	/// Starts modification of the current picker, using the new passed-in picker.
	/// </summary>
	/// <param name="newPicker">The new picker. This should be a clone of the original. A property will be modified on it after this completes.</param>
	public void ModifyPicker(Picker newPicker)
	{
		Trace.Assert(!this.SelectedPicker.Picker.IsModified, "Cannot start modification on already modified picker.");
		Trace.Assert(this.SelectedPicker.OriginalPicker == null, "Picker already has OriginalPicker.");

		if (this.SelectedPicker.Picker.IsModified || this.SelectedPicker.OriginalPicker != null)
		{
			return;
		}

		this.SelectedPicker.OriginalPicker = this.SelectedPicker.Picker;
		this.SelectedPicker.Picker = newPicker;
		this.SelectedPicker.Picker.IsModified = true;

		this.RefreshPickerButton();
	}

	public void SavePickersToStorage()
	{
		List<Picker> storagePickers = new();
		foreach (PickerViewModel pickerVM in this.pickers)
		{
			if (!pickerVM.Picker.IsDefault)
			{
				// Add the picker
				storagePickers.Add(pickerVM.Picker);

				// If it's modified, add the original one in as well.
				if (pickerVM.Picker.IsModified)
				{
					Trace.Assert(pickerVM.OriginalPicker != null, "Error saving pickers: Picker marked as modified but no OriginalPicker could be found.");
					if (pickerVM.OriginalPicker != null)
					{
						var originalPicker = new Picker();
						originalPicker.InjectFrom<CloneInjection>(pickerVM.OriginalPicker);
						originalPicker.IsModified = false;

						storagePickers.Add(originalPicker);
					}
				}
			}
		}

		PickerStorage.PickerList = storagePickers;
	}

	private void InsertNewPicker(int insertionIndex, PickerViewModel newPickerVM)
	{
		this.executingNonDragAdd = true;

		try
		{
			this.Pickers.Insert(insertionIndex, newPickerVM);
		}
		finally
		{
			this.executingNonDragAdd = false;
		}
	}

	private void RefreshPickerButton()
	{
		this.RaisePropertyChanged(nameof(this.PickerButtonMenuItems));
	}

	private void NotifySelectedPickerChanged()
	{
		this.RaisePropertyChanged(nameof(this.SelectedPicker));
		this.RefreshPickerButton();

		this.outputPathService.GenerateOutputFileName();

		Config.LastPickerIndex = this.Pickers.IndexOf(this.selectedPicker);
	}
}
