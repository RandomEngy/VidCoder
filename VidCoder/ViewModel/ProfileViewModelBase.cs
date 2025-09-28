using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AnyContainer;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Services;
using VidCoderCommon.Model;
using VidCoderCommon.Utilities.Injection;

namespace VidCoder.ViewModel;

public class ProfileViewModelBase : ReactiveObject
{
	private MainViewModel mainViewModel = StaticResolver.Resolve<MainViewModel>();
	private PresetsService presetsService = StaticResolver.Resolve<PresetsService>();

	private Dictionary<string, Action<object>> profileProperties;

	protected ProfileViewModelBase()
	{
		this.profileProperties = new Dictionary<string, Action<object>>();

		this.presetsService.WhenAnyValue(x => x.SelectedPreset.Preset.EncodingProfile)
			.Subscribe(x =>
			{
				bool automaticChangePreviousValue = this.AutomaticChange;
				this.AutomaticChange = true;
				this.RaiseAllChanged();
				this.AutomaticChange = automaticChangePreviousValue;
			});

		this.mainViewModel.WhenAnyValue(x => x.HasVideoSource)
			.ToProperty(this, x => x.HasSourceData, out this.hasSourceData);
	}

	public PresetsService PresetsService => this.presetsService;

	public MainViewModel MainViewModel => this.mainViewModel;

	public Preset Preset => this.presetsService.SelectedPreset.Preset;

	public VCProfile Profile => this.presetsService.SelectedPreset.Preset.EncodingProfile;

	public bool AutomaticChange
	{
		get { return this.presetsService.AutomaticChange; }
		set { this.presetsService.AutomaticChange = value; }
	}

	private ObservableAsPropertyHelper<bool> hasSourceData;
	public bool HasSourceData => this.hasSourceData.Value;

	protected void RegisterProfileProperty(string propertyName)
	{
		Action<object> a = null;
		this.RegisterProfileProperty(propertyName, a);
	}

	protected void RegisterProfileProperty(string propertyName, Action action)
	{
		this.RegisterProfileProperty(propertyName, o => action());
	}

	protected void RegisterProfileProperty(string propertyName, Action<object> action)
	{
		this.profileProperties.Add(propertyName, action);
	}

	private void RaiseAllChanged()
	{
		foreach (string key in this.profileProperties.Keys)
		{
			this.RaisePropertyChanged(key);
		}
	}

	/// <summary>
	/// Updates the profile property.
	/// </summary>
	/// <typeparam name="T">The type of value to update.</typeparam>
	/// <param name="propertyName">The name of the property.</param>
	/// <param name="value">The new value.</param>
	/// <param name="raisePropertyChanged">True to raise the PropertyChanged event.</param>
	protected void UpdateProfileProperty<T>(string propertyName, T value, bool raisePropertyChanged = true)
	{
		this.UpdateProfileProperty(() => this.Profile, propertyName, propertyName, value, raisePropertyChanged);
	}

	/// <summary>
	/// Updates the profile property (power user version).
	/// </summary>
	/// <typeparam name="TProperty">The type of the value to update.</typeparam>
	/// <typeparam name="TModel">The type of the model to update it on.</typeparam>
	/// <param name="targetFunc">Func to get the target model.</param>
	/// <param name="propertyName">The name of the property on the model to update.</param>
	/// <param name="raisePropertyName">The name to use when raising the PropertyChanged event.</param>
	/// <param name="value">The new value.</param>
	/// <param name="raisePropertyChanged">True to raise the PropertyChanged event.</param>
	protected void UpdateProfileProperty<TProperty, TModel>(Func<TModel> targetFunc, string propertyName, string raisePropertyName, TProperty value, bool raisePropertyChanged = true)
	{
		TModel originalTarget = targetFunc();

		if (!this.profileProperties.ContainsKey(raisePropertyName))
		{
			throw new ArgumentException("UpdatePresetProperty called on " + raisePropertyName + " without registering.");
		}

		Type type = typeof(TModel);
		PropertyInfo property = type.GetProperty(propertyName);
		var oldValue = (TProperty)property.GetValue(originalTarget);

		if (oldValue != null && oldValue.Equals(value))
		{
			return;
		}

		bool presetModified = false;

		if (!this.AutomaticChange)
		{
			if (!this.Preset.IsModified)
			{
				// Clone the profile so we modify a different copy.
				VCProfile newProfile = new();
				newProfile.InjectFrom<CloneInjection>(this.Profile);

				if (!this.Preset.IsModified)
				{
					this.presetsService.PrepareModifyPreset(newProfile);
					presetModified = true;
				}
			}
		}

		// Update the value and raise PropertyChanged
		property.SetValue(targetFunc(), value);

		if (raisePropertyChanged)
		{
			this.RaisePropertyChanged(raisePropertyName);
		}

		if (presetModified)
		{
			this.presetsService.FinalizeModifyPreset();
		}

		if (!this.AutomaticChange)
		{
			// If we have an action registered to update dependent properties, do it
			Action<object> action = this.profileProperties[raisePropertyName];
			if (action != null)
			{
				// Protect against update loops with a flag
				this.AutomaticChange = true;
				action(oldValue);
				this.AutomaticChange = false;
			}

			this.presetsService.SaveUserPresets();
		}
	}

	/// <summary>
	/// Updates multiple profile properties.
	/// </summary>
	/// <typeparam name="TProperty">The type of the value to update.</typeparam>
	/// <typeparam name="TModel">The type of the model to update it on.</typeparam>
	/// <param name="targetFunc">Func to get the target model.</param>
	/// <param name="updateAction">An action to perform the update on the model.</param>
	/// <param name="raisePropertyName">The name to use when raising the PropertyChanged event.</param>
	/// <param name="value">The new value.</param>
	protected void UpdateProfileProperties<TProperty, TModel>(Func<TModel> targetFunc, Action<TModel, TProperty> updateAction, string raisePropertyName, TProperty value)
	{
		if (!this.profileProperties.ContainsKey(raisePropertyName))
		{
			throw new ArgumentException("UpdatePresetProperty called on " + raisePropertyName + " without registering.");
		}

		bool presetModified = false;

		if (!this.AutomaticChange)
		{
			if (!this.Preset.IsModified)
			{
				// Clone the profile so we modify a different copy.
				VCProfile newProfile = new();
				newProfile.InjectFrom<CloneInjection>(this.Profile);

				if (!this.Preset.IsModified)
				{
					this.presetsService.PrepareModifyPreset(newProfile);
					presetModified = true;
				}
			}
		}

		// Update the value and raise PropertyChanged
		updateAction(targetFunc(), value);

		this.RaisePropertyChanged(raisePropertyName);

		if (presetModified)
		{
			this.presetsService.FinalizeModifyPreset();
		}

		if (!this.AutomaticChange)
		{
			// If we have an action registered to update dependent properties, do it
			Action<object> action = this.profileProperties[raisePropertyName];
			if (action != null)
			{
				// Protect against update loops with a flag
				this.AutomaticChange = true;
				action(null);
				this.AutomaticChange = false;
			}

			this.presetsService.SaveUserPresets();
		}
	}
}
