using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using FastMember;
using Omu.ValueInjecter;
using ReactiveUI;
using VidCoder.Services;
using VidCoderCommon.Model;

namespace VidCoder.ViewModel
{
	public class ProfileViewModelBase : ReactiveObject
	{
		private static TypeAccessor typeAccessor = TypeAccessor.Create(typeof(VCProfile));

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
		}

		public PresetsService PresetsService
		{
			get { return this.presetsService; }
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

		protected void UpdateProfileProperty<T>(Expression<Func<T>> propertyExpression, T value, bool raisePropertyChanged = true)
		{
			string propertyName = MvvmUtilities.GetPropertyName(propertyExpression);

			if (!this.profileProperties.ContainsKey(propertyName))
			{
				throw new ArgumentException("UpdatePresetProperty called on " + propertyName + " without registering.");
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
			typeAccessor[this.Profile, propertyName] = value;

			if (raisePropertyChanged)
			{
				this.RaisePropertyChanged(propertyName);
			}

			if (!this.AutomaticChange)
			{
				// If we have an action registered to update dependent properties, do it
				Action action = this.profileProperties[propertyName];
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
