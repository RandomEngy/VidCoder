using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using FastMember;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class ProfileViewModelBase : ReactiveObject
	{
		private MainViewModel mainViewModel = Ioc.Get<MainViewModel>();
		private PresetsService presetsService = Ioc.Get<PresetsService>();

		private Dictionary<string, Action> profileProperties;

		protected ProfileViewModelBase()
		{
			this.profileProperties = new Dictionary<string, Action>();

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

		public PresetsService PresetsService
		{
			get { return this.presetsService; }
		}

		public MainViewModel MainViewModel
		{
			get { return this.mainViewModel; }
		}

		public Preset Preset
		{
			get { return this.presetsService.SelectedPreset.Preset; }
		}

		public VCProfile Profile
		{
			get { return this.presetsService.SelectedPreset.Preset.EncodingProfile; }
		}

		public bool AutomaticChange
		{
			get { return this.presetsService.AutomaticChange; }
			set { this.presetsService.AutomaticChange = value; }
		}

		private ObservableAsPropertyHelper<bool> hasSourceData;
		public bool HasSourceData
		{
			get { return this.hasSourceData.Value; }
		}

		protected void RegisterProfileProperty<T>(Expression<Func<T>> propertyExpression, Action action = null)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);
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
		/// <param name="propertyExpression">Expression to get the property value.</param>
		/// <param name="value">The new value.</param>
		/// <param name="raisePropertyChanged">True to raise the PropertyChanged event.</param>
		protected void UpdateProfileProperty<T>(Expression<Func<T>> propertyExpression, T value, bool raisePropertyChanged = true)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);
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
			TypeAccessor typeAccessor = TypeAccessor.Create(typeof(TModel));

			if (!this.profileProperties.ContainsKey(raisePropertyName))
			{
				throw new ArgumentException("UpdatePresetProperty called on " + raisePropertyName + " without registering.");
			}

			if (((TProperty)typeAccessor[originalTarget, propertyName]).Equals(value))
			{
				return;
			}

			if (!this.AutomaticChange)
			{
				if (!this.Preset.IsModified)
				{
					// Clone the profile so we modify a different copy.
					VCProfile newProfile = new VCProfile();
					newProfile.InjectFrom(this.Profile);

					if (!this.Preset.IsModified)
					{
						this.presetsService.ModifyPreset(newProfile);
					}
				}
			}

			// Update the value and raise PropertyChanged
			typeAccessor[targetFunc(), propertyName] = value;

			if (raisePropertyChanged)
			{
				this.RaisePropertyChanged(raisePropertyName);
			}

			if (!this.AutomaticChange)
			{
				// If we have an action registered to update dependent properties, do it
				Action action = this.profileProperties[raisePropertyName];
				if (action != null)
				{
					// Protect against update loops with a flag
					this.AutomaticChange = true;
					action();
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

			if (!this.AutomaticChange)
			{
				if (!this.Preset.IsModified)
				{
					// Clone the profile so we modify a different copy.
					VCProfile newProfile = new VCProfile();
					newProfile.InjectFrom(this.Profile);

					if (!this.Preset.IsModified)
					{
						this.presetsService.ModifyPreset(newProfile);
					}
				}
			}

			// Update the value and raise PropertyChanged
			updateAction(targetFunc(), value);

			this.RaisePropertyChanged(raisePropertyName);

			if (!this.AutomaticChange)
			{
				// If we have an action registered to update dependent properties, do it
				Action action = this.profileProperties[raisePropertyName];
				if (action != null)
				{
					// Protect against update loops with a flag
					this.AutomaticChange = true;
					action();
					this.AutomaticChange = false;
				}

				this.presetsService.SaveUserPresets();
			}
		}
	}
}
