using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoder.Resources;

namespace VidCoder.ViewModel.DataModels;

public class WordBreakCharacterChoice : ReactiveObject
{
	private readonly PickerWindowViewModel pickerWindowViewModel;

	public WordBreakCharacterChoice(PickerWindowViewModel pickerWindowViewModel, bool isSelected)
	{
		this.pickerWindowViewModel = pickerWindowViewModel;
		this.isSelected = isSelected;
	}

	/// <summary>
	/// The word break character.
	/// </summary>
	public string Character { get; set; }

	/// <summary>
	/// The word for the character. Used for the screen reader and when DisplayUsingWord is true.
	/// </summary>
	public string CharacterWord { get; set; }

	/// <summary>
	/// True if we should display the character using the word instead.
	/// </summary>
	public bool DisplayUsingWord { get; set; }

	public string Display
	{
		get
		{
			return this.DisplayUsingWord ? this.CharacterWord : this.Character;
		}
	}

	public string AutomationName => string.Format(PickerRes.WordBreakCharacterAutomationNameFormat, this.CharacterWord);

	private bool isSelected;

	public bool IsSelected
	{
		get => this.isSelected;
		set
		{
			this.RaiseAndSetIfChanged(ref this.isSelected, value);
			this.pickerWindowViewModel.HandleWordBreakCharacterUpdate();
		}
	}
}
