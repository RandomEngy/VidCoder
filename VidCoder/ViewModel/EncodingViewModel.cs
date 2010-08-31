using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VidCoder.Model;
using HandBrake.SourceData;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Services;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using HandBrake.Interop;
using System.Windows;

namespace VidCoder.ViewModel
{
	public class EncodingViewModel : OkCancelDialogViewModel
	{
		private const int DimensionsAutoSetModulus = 16;

		private MainViewModel mainViewModel;

		private EncodingProfile profile;

		/// <summary>
		/// If true, indicates that this is not a user-initiated change.
		/// </summary>
		private bool automaticChange;

		private bool mp4ExtensionEnabled;

		private string outputSourceResolution;
		private string outputPixelAspectRatio;
		private string outputDisplayResolution;

		private Preset originalPreset;
		private bool isModified;
		private bool isBuiltIn;

		private List<int> chosenAudioTracks;

		private ObservableCollection<VideoEncoderViewModel> encoderChoices;
		private VideoEncoderViewModel selectedEncoder;
		private List<double> framerateChoices;

		private ObservableCollection<AudioEncodingViewModel> audioEncodings;
		private ObservableCollection<AudioOutputPreview> audioOutputPreviews;

		private AdvancedChoice referenceFrames;
		private AdvancedChoice bFrames;
		private AdvancedChoice adaptiveBFrames;
		private AdvancedChoice directPrediction;
		private bool weightedPFrames;
		private AdvancedChoice pyramidalBFrames;
		private AdvancedChoice motionEstimationMethod;
		private AdvancedChoice subpixelMotionEstimation;
		private AdvancedChoice motionEstimationRange;
		private AdvancedChoice analysis;
		private bool eightByEightDct;
		private bool cabacEntropyCoding;
		private AdvancedChoice trellis;
		private double adaptiveQuantizationStrength;
		private double psychovisualRateDistortion;
		private double psychovisualTrellis;
		private AdvancedChoice deblockingStrength;
		private AdvancedChoice deblockingThreshold;
		private bool noDctDecimate;

		/// <summary>
		/// X264 options that have UI elements that correspond to them.
		/// </summary>
		private HashSet<string> uiOptions = new HashSet<string>
		{
			"ref", "bframes", "b-adapt", "direct", "weightp", "b-pyramid", "me", "subme", "subq", "merange",
			"analyse", "8x8dct", "cabac", "trellis", "aq-strength", "psy-rd", "no-dct-decimate", "deblock"
		};

		private ICommand saveCommand;
		private ICommand saveAsCommand;
		private ICommand renameCommand;
		private ICommand deletePresetCommand;
		private ICommand previewCommand;
		private ICommand addAudioEncodingCommand;

		public EncodingViewModel(Preset preset, MainViewModel mainViewModel)
		{
			this.mainViewModel = mainViewModel;
			
			this.encoderChoices = new ObservableCollection<VideoEncoderViewModel>();
			this.encoderChoices.Add(new VideoEncoderViewModel { Encoder = VideoEncoder.FFMpeg, Display = "MPEG-4 (FFMpeg)" });
			this.encoderChoices.Add(new VideoEncoderViewModel { Encoder = VideoEncoder.X264, Display = "H.264 (x264)" });
			this.audioOutputPreviews = new ObservableCollection<AudioOutputPreview>();
			this.audioEncodings = new ObservableCollection<AudioEncodingViewModel>();

			this.EditingPreset = preset;
			this.mainViewModel.PropertyChanged += this.OnMainPropertyChanged;
			this.mainViewModel.AudioChoices.CollectionChanged += this.AudioChoicesCollectionChanged;
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public Preset EditingPreset
		{
			get
			{
				return this.originalPreset;
			}

			set
			{
				if (value.IsModified)
				{
					// If already modified, use the existing profile.
					this.profile = value.EncodingProfile;
				}
				else
				{
					// If not modified, clone the profile
					this.profile = value.EncodingProfile.Clone();
				}

				this.originalPreset = value;

				this.IsBuiltIn = value.IsBuiltIn;
				this.isModified = value.IsModified;

				if (this.encoderChoices.Count > 2)
				{
					this.encoderChoices.RemoveAt(2);
				}

				if (this.profile.OutputFormat == OutputFormat.Mkv)
				{
					this.encoderChoices.Add(new VideoEncoderViewModel { Encoder = VideoEncoder.Theora, Display = "VP3 (Theora)" });
				}

				this.selectedEncoder = this.encoderChoices.Single(encoderChoice => encoderChoice.Encoder == this.profile.VideoEncoder);
				this.framerateChoices = new List<double>
				{
					0,
					5,
					10,
					15,
					23.976,
					24,
					25,
					29.97
				};

				this.chosenAudioTracks = this.mainViewModel.GetChosenAudioTracks();
				this.audioEncodings.Clear();
				foreach (AudioEncoding audioEncoding in this.profile.AudioEncodings)
				{
					this.audioEncodings.Add(new AudioEncodingViewModel(audioEncoding, this.mainViewModel.SelectedTitle, this.chosenAudioTracks, this.profile.OutputFormat, this));
				}

				this.audioOutputPreviews.Clear();
				this.RefreshAudioPreview();
				this.RefreshOutputSize();
				this.UpdateAudioEncodings();
				this.UpdateUIFromAdvancedOptions();

				this.NotifyAllChanged();
			}
		}

		public EncodingProfile EncodingProfile
		{
			get
			{
				return this.profile;
			}
		}

		public string WindowTitle
		{
			get
			{
				string windowTitle = "Preset: " + this.ProfileName;
				if (this.IsModified)
				{
					windowTitle += " *";
				}

				return windowTitle;
			}
		}

		public Title SelectedTitle
		{
			get
			{
				return this.mainViewModel.SelectedTitle;
			}
		}

		public string ProfileName
		{
			get
			{
				return this.originalPreset.Name;
			}
		}

		public bool IsBuiltIn
		{
			get
			{
				return this.isBuiltIn;
			}

			set
			{
				this.isBuiltIn = value;
				this.NotifyPropertyChanged("IsBuiltIn");
				this.NotifyPropertyChanged("DeleteButtonVisible");
			}
		}

		public bool DeleteButtonVisible
		{
			get
			{
				return !this.IsBuiltIn && !this.IsModified;
			}
		}

		public bool IsModified
		{
			get
			{
				return this.originalPreset.IsModified;
			}

			set
			{
				// Don't mark as modified if this is an automatic change.
				if (!this.automaticChange)
				{
					if (this.originalPreset.IsModified != value)
					{
						if (value)
						{
							this.mainViewModel.ModifyPreset(this.profile);
						}
					}

					this.NotifyPropertyChanged("IsModified");
					this.NotifyPropertyChanged("WindowTitle");
					this.NotifyPropertyChanged("DeleteButtonVisible");

					// If we've made a modification, we need to save the user presets.
					if (value)
					{
						this.mainViewModel.SaveUserPresets();
					}
				}
			}
		}

		public bool HasSourceData
		{
			get
			{
				return this.mainViewModel.SelectedTitle != null;
			}
		}

		public OutputFormat OutputFormat
		{
			get
			{
				return this.profile.OutputFormat;
			}

			set
			{
				this.profile.OutputFormat = value;
				this.NotifyPropertyChanged("OutputFormat");
				this.NotifyPropertyChanged("ShowMp4Choices");
				this.IsModified = true;
				this.mainViewModel.RefreshDestination();

				// Report output format change to audio encodings.
				foreach (AudioEncodingViewModel audioEncoding in this.AudioEncodings)
				{
					audioEncoding.OutputFormat = value;
				}

				if (value == OutputFormat.Mkv)
				{
					if (this.EncoderChoices.Count < 3)
					{
						this.EncoderChoices.Add(new VideoEncoderViewModel { Encoder = VideoEncoder.Theora, Display = "VP3 (Theora)" });
					}
				}
				else
				{
					if (this.EncoderChoices.Count == 3)
					{
						this.EncoderChoices.RemoveAt(2);

						if (this.SelectedEncoder == null)
						{
							this.SelectedEncoder = this.EncoderChoices[1];
						}
					}
				}
			}
		}

		public OutputExtension PreferredExtension
		{
			get
			{
				return this.profile.PreferredExtension;
			}

			set
			{
				this.profile.PreferredExtension = value;
				this.NotifyPropertyChanged("PreferredExtension");
				this.IsModified = true;
				this.mainViewModel.RefreshDestination();
			}
		}

		public void RefreshExtensionChoice()
		{
			if (this.OutputFormat != OutputFormat.Mp4)
			{
				return;
			}

			bool enableMp4 = true;
			foreach (AudioEncodingViewModel audioVM in this.AudioEncodings)
			{
				if (audioVM.SelectedAudioEncoder.Encoder == AudioEncoder.Ac3Passthrough)
				{
					enableMp4 = false;
					break;
				}
			}

			this.Mp4ExtensionEnabled = enableMp4;

			if (!enableMp4 && this.PreferredExtension == OutputExtension.Mp4)
			{
				this.PreferredExtension = OutputExtension.M4v;
			}
		}

		public bool Mp4ExtensionEnabled
		{
			get
			{
				return this.mp4ExtensionEnabled;
			}

			set
			{
				this.mp4ExtensionEnabled = value;
				this.NotifyPropertyChanged("Mp4ExtensionEnabled");
			}
		}

		public bool LargeFile
		{
			get
			{
				return this.profile.LargeFile;
			}

			set
			{
				this.profile.LargeFile = value;
				this.NotifyPropertyChanged("LargeFile");
				this.IsModified = true;
			}
		}

		public bool Optimize
		{
			get
			{
				return this.profile.Optimize;
			}

			set
			{
				this.profile.Optimize = value;
				this.NotifyPropertyChanged("Optimize");
				this.IsModified = true;
			}
		}

		public bool IPod5GSupport
		{
			get
			{
				return this.profile.IPod5GSupport;
			}

			set
			{
				this.profile.IPod5GSupport = value;
				this.NotifyPropertyChanged("IPod5GSupport");
				this.IsModified = true;
			}
		}

		public bool ShowMp4Choices
		{
			get
			{
				return this.OutputFormat == OutputFormat.Mp4;
			}
		}

		public bool IncludeChapterMarkers
		{
			get
			{
				return this.profile.IncludeChapterMarkers;
			}

			set
			{
				this.profile.IncludeChapterMarkers = value;
				this.NotifyPropertyChanged("IncludeChapterMarkers");
				this.IsModified = true;
				this.mainViewModel.RefreshChapterMarkerUI();
			}
		}

		public ICommand SaveCommand
		{
			get
			{
				if (this.saveCommand == null)
				{
					this.saveCommand = new RelayCommand(param =>
					{
						this.mainViewModel.SavePreset();
						this.IsModified = false;

						// Clone the profile so that on modifications, we're working on a new copy.
						this.profile = this.profile.Clone();
					}, param =>
					{
						return !this.IsBuiltIn;
					});
				}

				return this.saveCommand;
			}
		}

		public ICommand SaveAsCommand
		{
			get
			{
				if (this.saveAsCommand == null)
				{
					this.saveAsCommand = new RelayCommand(param =>
					{
						var dialogVM = new ChoosePresetNameViewModel(this.mainViewModel.AllPresets);
						dialogVM.PresetName = this.originalPreset.Name;
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.PresetName;

							this.mainViewModel.SavePresetAs(newPresetName);

							this.NotifyPropertyChanged("ProfileName");
							this.NotifyPropertyChanged("WindowTitle");

							this.IsModified = false;
							this.IsBuiltIn = false;
						}
					});
				}

				return this.saveAsCommand;
			}
		}

		public ICommand RenameCommand
		{
			get
			{
				if (this.renameCommand == null)
				{
					this.renameCommand = new RelayCommand(param =>
					{
						var dialogVM = new ChoosePresetNameViewModel(this.mainViewModel.AllPresets);
						dialogVM.PresetName = this.originalPreset.Name;
						WindowManager.OpenDialog(dialogVM, this);

						if (dialogVM.DialogResult)
						{
							string newPresetName = dialogVM.PresetName;
							this.originalPreset.Name = newPresetName;

							this.mainViewModel.SavePreset();

							this.NotifyPropertyChanged("ProfileName");
							this.NotifyPropertyChanged("WindowTitle");

							this.IsModified = false;
						}
					},
					param =>
					{
						return !this.IsBuiltIn;
					});
				}

				return this.renameCommand;
			}
		}

		public ICommand DeletePresetCommand
		{
			get
			{
				if (this.deletePresetCommand == null)
				{
					this.deletePresetCommand = new RelayCommand(param =>
					{
						if (this.IsModified)
						{
							MessageBoxResult dialogResult = ServiceFactory.MessageBoxService.Show(this, "Are you sure you want to revert all unsaved changes to this preset?", "Revert Preset", MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.mainViewModel.RevertPreset(true);
							}

							//this.IsModified = false;
							this.EditingPreset = this.originalPreset;
						}
						else
						{
							MessageBoxResult dialogResult = ServiceFactory.MessageBoxService.Show(this, "Are you sure you want to remove this preset?", "Remove Preset", MessageBoxButton.YesNo);
							if (dialogResult == MessageBoxResult.Yes)
							{
								this.mainViewModel.DeletePreset();
							}
						}
					},
					param =>
					{
						// We can delete or revert if it's a user preset or if there have been modifications.
						return !this.IsBuiltIn || this.IsModified;
					});
				}

				return this.deletePresetCommand;
			}
		}

		public ICommand PreviewCommand
		{
			get
			{
				if (this.previewCommand == null)
				{
					this.previewCommand = new RelayCommand(param =>
					{
						this.mainViewModel.OpenPreviewWindowCommand.Execute(null);
					});
				}

				return this.previewCommand;
			}
		}

		#region Picture

		public string InputSourceResolution
		{
			get
			{
				if (this.HasSourceData)
				{
					return this.SelectedTitle.Resolution.Width + " x " + this.SelectedTitle.Resolution.Height;
				}

				return string.Empty;
			}
		}

		public string InputPixelAspectRatio
		{
			get
			{
				if (this.HasSourceData)
				{
					return this.CreateParDisplayString(this.SelectedTitle.ParVal.Width, this.SelectedTitle.ParVal.Height);
				}

				return string.Empty;
			}
		}

		public string InputDisplayResolution
		{
			get
			{
				if (this.HasSourceData)
				{
					double pixelAspectRatio = ((double)this.SelectedTitle.ParVal.Width) / this.SelectedTitle.ParVal.Height;
					double displayWidth = this.SelectedTitle.Resolution.Width * pixelAspectRatio;
					int displayWidthRounded = (int)Math.Round(displayWidth);

					return displayWidthRounded + " x " + this.SelectedTitle.Resolution.Height;
				}

				return string.Empty;
			}
		}

		public string OutputSourceResolution
		{
			get
			{
				return this.outputSourceResolution;
			}

			set
			{
				this.outputSourceResolution = value;
				this.NotifyPropertyChanged("OutputSourceResolution");
			}
		}

		public string OutputPixelAspectRatio
		{
			get
			{
				return this.outputPixelAspectRatio;
			}

			set
			{
				this.outputPixelAspectRatio = value;
				this.NotifyPropertyChanged("OutputPixelAspectRatio");
			}
		}

		public string OutputDisplayResolution
		{
			get
			{
				return this.outputDisplayResolution;
			}

			set
			{
				this.outputDisplayResolution = value;
				this.NotifyPropertyChanged("OutputDisplayResolution");
			}
		}

		public int Width
		{
			get
			{
				return this.profile.Width;
			}

			set
			{
				if (this.profile.Width != value)
				{
					this.profile.Width = value;
					this.NotifyPropertyChanged("Width");
					if (this.Anamorphic == Anamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Height > 0 && value > 0)
					{
						var cropWidthAmount = this.CropLeft + this.CropRight;
						var cropHeightAmount = this.CropTop + this.CropBottom;
						var sourceWidth = this.SelectedTitle.Resolution.Width;
						var sourceHeight = this.SelectedTitle.Resolution.Height;
						var parWidth = this.SelectedTitle.ParVal.Width;
						var parHeight = this.SelectedTitle.ParVal.Height;

						int newHeight = (sourceHeight * parHeight * (value + cropWidthAmount) - cropHeightAmount * sourceWidth * parWidth) /
									  (sourceWidth * parWidth);
						newHeight = this.GetNearestValue(newHeight, DimensionsAutoSetModulus);
						if (newHeight > 0)
						{
							this.profile.Height = newHeight;
							this.NotifyPropertyChanged("Height");
						}
					}

					this.UpdatePreviewWindow();
					this.RefreshOutputSize();
					this.IsModified = true;
				}
			}
		}

		public bool WidthEnabled
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict)
				{
					return false;
				}

				return true;
			}
		}

		public int Height
		{
			get
			{
				return this.profile.Height;
			}

			set
			{
				if (this.profile.Height != value)
				{
					this.profile.Height = value;
					this.NotifyPropertyChanged("Height");
					if (this.Anamorphic == Anamorphic.None && this.KeepDisplayAspect && this.HasSourceData && this.Width > 0 && value > 0)
					{
						var cropWidthAmount = this.CropLeft + this.CropRight;
						var cropHeightAmount = this.CropTop + this.CropBottom;
						var sourceWidth = this.SelectedTitle.Resolution.Width;
						var sourceHeight = this.SelectedTitle.Resolution.Height;
						var parWidth = this.SelectedTitle.ParVal.Width;
						var parHeight = this.SelectedTitle.ParVal.Height;

						int newWidth = (sourceWidth * parWidth * (value + cropHeightAmount) - cropWidthAmount * sourceHeight * parHeight) /
									 (sourceHeight * parHeight);
						newWidth = this.GetNearestValue(newWidth, DimensionsAutoSetModulus);
						if (newWidth > 0)
						{
							this.profile.Width = newWidth;
							this.NotifyPropertyChanged("Width");
						}
					}

					this.UpdatePreviewWindow();
					this.RefreshOutputSize();
					this.IsModified = true;
				}
			}
		}

		public bool HeightEnabled
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict || this.Anamorphic == Anamorphic.Loose)
				{
					return false;
				}

				return true;
			}
		}

		public int MaxWidth
		{
			get
			{
				return this.profile.MaxWidth;
			}

			set
			{
				if (this.profile.MaxWidth != value)
				{
					this.profile.MaxWidth = value;
					this.NotifyPropertyChanged("MaxWidth");
					this.UpdatePreviewWindow();
					this.RefreshOutputSize();
					this.IsModified = true;
				}
			}
		}

		public int MaxHeight
		{
			get
			{
				return this.profile.MaxHeight;
			}

			set
			{
				if (this.profile.MaxHeight != value)
				{
					this.profile.MaxHeight = value;
					this.NotifyPropertyChanged("MaxHeight");
					this.UpdatePreviewWindow();
					this.RefreshOutputSize();
					this.IsModified = true;
				}
			}
		}

		public bool KeepDisplayAspect
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict || this.Anamorphic == Anamorphic.Loose)
				{
					return true;
				}

				if (this.Anamorphic == Anamorphic.Custom && !this.UseDisplayWidth)
				{
					return false;
				}

				return this.profile.KeepDisplayAspect;
			}

			set
			{
				this.profile.KeepDisplayAspect = value;
				this.NotifyPropertyChanged("KeepDisplayAspect");
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
				this.IsModified = true;
			}
		}

		public bool KeepDisplayAspectEnabled
		{
			get
			{
				if (this.Anamorphic == Anamorphic.Strict || this.Anamorphic == Anamorphic.Loose || this.Anamorphic == Anamorphic.Custom && !this.UseDisplayWidth)
				{
					return false;
				}

				return true;
			}
		}

		public Anamorphic Anamorphic
		{
			get
			{
				return this.profile.Anamorphic;
			}

			set
			{
				this.profile.Anamorphic = value;

				if (value == Anamorphic.Strict || value == Anamorphic.Loose)
				{
					this.KeepDisplayAspect = true;
				}

				if (this.profile.Anamorphic == Anamorphic.Custom && !this.profile.UseDisplayWidth)
				{
					this.PopulatePixelAspect();
				}

				this.NotifyPropertyChanged("Anamorphic");
				this.NotifyPropertyChanged("CustomAnamorphicFieldsVisible");
				this.NotifyPropertyChanged("KeepDisplayAspect");
				this.NotifyPropertyChanged("KeepDisplayAspectEnabled");
				this.NotifyPropertyChanged("WidthEnabled");
				this.NotifyPropertyChanged("HeightEnabled");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		public bool CustomAnamorphicFieldsVisible
		{
			get
			{
				return this.Anamorphic == Anamorphic.Custom;
			}
		}

		public int Modulus
		{
			get
			{
				return this.profile.Modulus;
			}

			set
			{
				this.profile.Modulus = value;
				this.UpdatePreviewWindow();
				this.NotifyPropertyChanged("Modulus");
			}
		}

		public bool UseDisplayWidth
		{
			get
			{
				return this.profile.UseDisplayWidth;
			}

			set
			{
				this.profile.UseDisplayWidth = value;
				if (!value)
				{
					this.PopulatePixelAspect();
				}

				this.NotifyPropertyChanged("UseDisplayWidth");
				this.NotifyPropertyChanged("KeepDisplayAspect");
				this.NotifyPropertyChanged("KeepDisplayAspectEnabled");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		public int DisplayWidth
		{
			get
			{
				return this.profile.DisplayWidth;
			}

			set
			{
				this.profile.DisplayWidth = value;
				this.NotifyPropertyChanged("DisplayWidth");
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
				this.IsModified = true;
			}
		}

		public int PixelAspectX
		{
			get
			{
				return this.profile.PixelAspectX;
			}

			set
			{
				this.profile.PixelAspectX = value;
				this.NotifyPropertyChanged("PixelAspectX");
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
				this.IsModified = true;
			}
		}

		public int PixelAspectY
		{
			get
			{
				return this.profile.PixelAspectY;
			}

			set
			{
				this.profile.PixelAspectY = value;
				this.NotifyPropertyChanged("PixelAspectY");
				this.RefreshOutputSize();
				this.UpdatePreviewWindow();
				this.IsModified = true;
			}
		}

		public bool CustomCropping
		{
			get
			{
				return this.profile.CustomCropping;
			}

			set
			{
				this.profile.CustomCropping = value;

				this.NotifyPropertyChanged("CustomCropping");
				this.NotifyPropertyChanged("CropLeft");
				this.NotifyPropertyChanged("CropTop");
				this.NotifyPropertyChanged("CropRight");
				this.NotifyPropertyChanged("CropBottom");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		public int CropLeft
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.profile.Cropping.Left;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Left;
				}

				return 0;
			}

			set
			{
				this.profile.Cropping.Left = value;
				this.NotifyPropertyChanged("CropLeft");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		public int CropTop
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.profile.Cropping.Top;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Top;
				}

				return 0;
			}

			set
			{
				this.profile.Cropping.Top = value;
				this.NotifyPropertyChanged("CropTop");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		public int CropRight
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.profile.Cropping.Right;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Right;
				}

				return 0;
			}

			set
			{
				this.profile.Cropping.Right = value;
				this.NotifyPropertyChanged("CropRight");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		public int CropBottom
		{
			get
			{
				if (this.CustomCropping)
				{
					return this.profile.Cropping.Bottom;
				}

				if (this.HasSourceData)
				{
					return this.SelectedTitle.AutoCropDimensions.Bottom;
				}

				return 0;
			}

			set
			{
				this.profile.Cropping.Bottom = value;
				this.NotifyPropertyChanged("CropBottom");
				this.UpdatePreviewWindow();
				this.RefreshOutputSize();
				this.IsModified = true;
			}
		}

		#endregion

		#region Filters

		public Detelecine Detelecine
		{
			get
			{
				return this.profile.Detelecine;
			}

			set
			{
				this.profile.Detelecine = value;
				this.NotifyPropertyChanged("Detelecine");
				this.NotifyPropertyChanged("CustomDetelecineVisible");
				this.IsModified = true;
			}
		}

		public string CustomDetelecine
		{
			get
			{
				return this.profile.CustomDetelecine;
			}

			set
			{
				this.profile.CustomDetelecine = value;
				this.NotifyPropertyChanged("CustomDetelecine");
				this.IsModified = true;
			}
		}

		public bool CustomDetelecineVisible
		{
			get
			{
				return this.Detelecine == Detelecine.Custom;
			}
		}

		public Deinterlace Deinterlace
		{
			get
			{
				return this.profile.Deinterlace;
			}

			set
			{
				this.profile.Deinterlace = value;
				if (value != Deinterlace.Off)
				{
					this.Decomb = Decomb.Off;
				}

				this.NotifyPropertyChanged("Deinterlace");
				this.NotifyPropertyChanged("CustomDeinterlaceVisible");
				this.UpdatePreviewWindow();
				this.IsModified = true;
			}
		}

		public string CustomDeinterlace
		{
			get
			{
				return this.profile.CustomDeinterlace;
			}

			set
			{
				this.profile.CustomDeinterlace = value;
				this.NotifyPropertyChanged("CustomDeinterlace");
				this.IsModified = true;
			}
		}

		public bool CustomDeinterlaceVisible
		{
			get
			{
				return this.Deinterlace == Deinterlace.Custom;
			}
		}

		public Decomb Decomb
		{
			get
			{
				return this.profile.Decomb;
			}

			set
			{
				this.profile.Decomb = value;
				if (value != Decomb.Off)
				{
					this.Deinterlace = Deinterlace.Off;
				}

				this.NotifyPropertyChanged("Decomb");
				this.NotifyPropertyChanged("CustomDecombVisible");
				this.IsModified = true;
			}
		}

		public string CustomDecomb
		{
			get
			{
				return this.profile.CustomDecomb;
			}

			set
			{
				this.profile.CustomDecomb = value;
				this.NotifyPropertyChanged("CustomDecomb");
				this.IsModified = true;
			}
		}

		public bool CustomDecombVisible
		{
			get
			{
				return this.Decomb == Decomb.Custom;
			}
		}

		public Denoise Denoise
		{
			get
			{
				return this.profile.Denoise;
			}

			set
			{
				this.profile.Denoise = value;
				this.NotifyPropertyChanged("Denoise");
				this.NotifyPropertyChanged("CustomDenoiseVisible");
				this.IsModified = true;
			}
		}

		public string CustomDenoise
		{
			get
			{
				return this.profile.CustomDenoise;
			}

			set
			{
				this.profile.CustomDenoise = value;
				this.NotifyPropertyChanged("CustomDenoise");
				this.IsModified = true;
			}
		}

		public bool CustomDenoiseVisible
		{
			get
			{
				return this.Denoise == Denoise.Custom;
			}
		}

		public int Deblock
		{
			get
			{
				return this.profile.Deblock;
			}

			set
			{
				this.profile.Deblock = value;
				this.NotifyPropertyChanged("Deblock");
				this.NotifyPropertyChanged("DeblockText");
				this.IsModified = true;
			}
		}

		public string DeblockText
		{
			get
			{
				if (this.Deblock >= 5)
				{
					return this.Deblock.ToString();
				}

				return "Off";
			}
		}

		public bool Grayscale
		{
			get
			{
				return this.profile.Grayscale;
			}

			set
			{
				this.profile.Grayscale = value;
				this.NotifyPropertyChanged("Grayscale");
				this.IsModified = true;
			}
		}

		#endregion

		#region Video

		public ObservableCollection<VideoEncoderViewModel> EncoderChoices
		{
			get
			{
				return this.encoderChoices;
			}
		}

		public VideoEncoderViewModel SelectedEncoder
		{
			get
			{
				return this.selectedEncoder;
			}

			set
			{
				if (value != null && value != this.selectedEncoder)
				{
					VideoEncoderViewModel oldEncoder = this.selectedEncoder;

					this.selectedEncoder = value;
					this.profile.VideoEncoder = this.selectedEncoder.Encoder;
					this.NotifyPropertyChanged("SelectedEncoder");
					this.NotifyPropertyChanged("QualitySliderMin");
					this.NotifyPropertyChanged("QualitySliderMax");
					this.NotifyPropertyChanged("QualitySliderLeftText");
					this.NotifyPropertyChanged("QualitySliderRightText");
					this.IsModified = true;

					// Move the quality number to something equivalent for the new encoder.
					if (oldEncoder != null && value != null)
					{
						double oldQualityFraction = 0.0;

						switch (oldEncoder.Encoder)
						{
							case VideoEncoder.X264:
								oldQualityFraction = 1.0 - this.Quality / 51.0;
								break;
							case VideoEncoder.FFMpeg:
								oldQualityFraction = 1.0 - this.Quality / 31.0;
								break;
							case VideoEncoder.Theora:
								oldQualityFraction = this.Quality / 63.0;
								break;
							default:
								throw new InvalidOperationException("Unrecognized encoder.");
						}

						switch (value.Encoder)
						{
							case VideoEncoder.X264:
								this.Quality = Math.Round((1.0 - oldQualityFraction) * 51.0);
								break;
							case VideoEncoder.FFMpeg:
								this.Quality = Math.Max(1.0, Math.Round((1.0 - oldQualityFraction) * 31.0));
								break;
							case VideoEncoder.Theora:
								this.Quality = Math.Round(oldQualityFraction * 63);
								break;
							default:
								throw new InvalidOperationException("Unrecognized encoder.");
						}
					}
				}
			}
		}

		public List<double> FramerateChoices
		{
			get
			{
				return this.framerateChoices;
			}
		}

		public double SelectedFramerate
		{
			get
			{
				return this.profile.Framerate;
			}

			set
			{
				this.profile.Framerate = value;
				this.NotifyPropertyChanged("SelectedFramerate");
				this.NotifyPropertyChanged("PeakFramerateVisible");
				this.IsModified = true;
			}
		}

		public bool PeakFramerate
		{
			get
			{
				return this.profile.PeakFramerate;
			}

			set
			{
				this.profile.PeakFramerate = value;
				this.NotifyPropertyChanged("PeakFramerate");
				this.IsModified = true;
			}
		}

		public bool PeakFramerateVisible
		{
			get
			{
				return this.SelectedFramerate != 0;
			}
		}

		public bool TwoPassEncoding
		{
			get
			{
				return this.profile.TwoPass;
			}

			set
			{
				this.profile.TwoPass = value;
				this.NotifyPropertyChanged("TwoPassEncoding");
				this.NotifyPropertyChanged("TurboFirstPass");
				this.NotifyPropertyChanged("TurboFirstPassEnabled");
				this.IsModified = true;
			}
		}

		public bool TwoPassEncodingEnabled
		{
			get
			{
				return this.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality;
			}
		}

		public bool TurboFirstPass
		{
			get
			{
				if (!this.TwoPassEncoding)
				{
					return false;
				}

				return profile.TurboFirstPass;
			}

			set
			{
				this.profile.TurboFirstPass = value;
				this.NotifyPropertyChanged("TurboFirstPass");
				this.IsModified = true;
			}
		}

		public bool TurboFirstPassEnabled
		{
			get
			{
				return this.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality && this.TwoPassEncoding;
			}
		}

		public VideoEncodeRateType VideoEncodeRateType
		{
			get
			{
				return this.profile.VideoEncodeRateType;
			}

			set
			{
				this.profile.VideoEncodeRateType = value;
				this.NotifyPropertyChanged("VideoEncodeRateType");

				if (value == VideoEncodeRateType.ConstantQuality)
				{
					// Set up a default quality.

					switch (this.SelectedEncoder.Encoder)
					{
						case VideoEncoder.X264:
							this.Quality = 20;
							break;
						case VideoEncoder.FFMpeg:
							this.Quality = 12;
							break;
						case VideoEncoder.Theora:
							this.Quality = 38;
							break;
						default:
							break;
					}

					// Disable two-pass options

					this.profile.TwoPass = false;
					this.profile.TurboFirstPass = false;
					this.NotifyPropertyChanged("TwoPassEncoding");
					this.NotifyPropertyChanged("TurboFirstPass");
				}

				this.NotifyPropertyChanged("TwoPassEncodingEnabled");
				this.NotifyPropertyChanged("TurboFirstPassEnabled");

				if (value == VideoEncodeRateType.AverageBitrate)
				{
					this.VideoBitrate = 1200;
				}

				this.mainViewModel.RefreshDestination();
				this.IsModified = true;
			}
		}

		public int TargetSize
		{
			get
			{
				return this.profile.TargetSize;
			}

			set
			{
				this.profile.TargetSize = value;
				this.NotifyPropertyChanged("TargetSize");
				this.mainViewModel.RefreshDestination();
				this.IsModified = true;
			}
		}

		public int VideoBitrate
		{
			get
			{
				return this.profile.VideoBitrate;
			}

			set
			{
				this.profile.VideoBitrate = value;
				this.NotifyPropertyChanged("VideoBitrate");
				this.mainViewModel.RefreshDestination();
				this.IsModified = true;
			}
		}

		public double Quality
		{
			get
			{
				return this.profile.Quality;
			}

			set
			{
				this.profile.Quality = value;
				this.NotifyPropertyChanged("Quality");
				this.mainViewModel.RefreshDestination();
				this.IsModified = true;
			}
		}

		public int QualitySliderMin
		{
			get
			{
				switch (this.SelectedEncoder.Encoder)
				{
					case VideoEncoder.X264:
						return 0;
					case VideoEncoder.FFMpeg:
						return 1;
					case VideoEncoder.Theora:
						return 0;
					default:
						return 0;
				}
			}
		}

		public int QualitySliderMax
		{
			get
			{
				switch (this.SelectedEncoder.Encoder)
				{
					case VideoEncoder.X264:
						return 51;
					case VideoEncoder.FFMpeg:
						return 31;
					case VideoEncoder.Theora:
						return 63;
					default:
						return 0;
				}
			}
		}

		public string QualitySliderLeftText
		{
			get
			{
				switch (this.SelectedEncoder.Encoder)
				{
					case VideoEncoder.X264:
					case VideoEncoder.FFMpeg:
						return "High quality";
					case VideoEncoder.Theora:
						return "Low quality";
					default:
						return string.Empty;
				}
			}
		}

		public string QualitySliderRightText
		{
			get
			{
				switch (this.SelectedEncoder.Encoder)
				{
					case VideoEncoder.X264:
					case VideoEncoder.FFMpeg:
						return "Low quality";
					case VideoEncoder.Theora:
						return "High quality";
					default:
						return string.Empty;
				}
			}
		}

		#endregion

		#region Audio

		public ObservableCollection<AudioEncodingViewModel> AudioEncodings
		{
			get
			{
				return this.audioEncodings;
			}
		}

		public ObservableCollection<AudioOutputPreview> AudioOutputPreviews
		{
			get
			{
				return this.audioOutputPreviews;
			}
		}

		public bool HasAudioTracks
		{
			get
			{
				return this.AudioOutputPreviews.Count > 0;
			}
		}

		public ICommand AddAudioEncodingCommand
		{
			get
			{
				if (this.addAudioEncodingCommand == null)
				{
					this.addAudioEncodingCommand = new RelayCommand(param =>
					{
						var newAudioEncoding = new AudioEncoding
						{
							InputNumber = 0,
							Encoder = AudioEncoder.Faac,
							Mixdown = Mixdown.DolbyProLogicII,
							Bitrate = 160,
							SampleRateRaw = 48000,
							Drc = 0.0
						};

						this.AudioEncodings.Add(new AudioEncodingViewModel(newAudioEncoding, this.mainViewModel.SelectedTitle, this.chosenAudioTracks, this.OutputFormat, this));
						this.NotifyPropertyChanged("HasAudioTracks");
						this.RefreshExtensionChoice();
						this.RefreshAudioPreview();
						this.UpdateAudioEncodings();
						this.IsModified = true;
					});
				}

				return this.addAudioEncodingCommand;
			}
		}

		public void RemoveAudioEncoding(AudioEncodingViewModel audioEncodingVM)
		{
			this.AudioEncodings.Remove(audioEncodingVM);
			this.NotifyPropertyChanged("HasAudioTracks");
			this.RefreshExtensionChoice();
			this.RefreshAudioPreview();
			this.UpdateAudioEncodings();
			this.IsModified = true;
		}

		public void RefreshAudioPreview()
		{
			if (this.SelectedTitle != null && this.AudioOutputPreviews != null)
			{
				this.AudioOutputPreviews.Clear();

				List<int> chosenAudioTracks = this.mainViewModel.GetChosenAudioTracks();

				int outputTrackNumber = 1;
				foreach (AudioEncodingViewModel audioVM in this.AudioEncodings)
				{
					AudioEncoder encoder = audioVM.SelectedAudioEncoder.Encoder;

					if (audioVM.TargetStreamIndex == 0)
					{
						foreach (AudioChoiceViewModel audioChoice in this.mainViewModel.AudioChoices)
						{
							var outputPreviewTrack = new AudioOutputPreview
							{
								TrackNumber = "#" + outputTrackNumber + ":",
								Name = this.SelectedTitle.AudioTracks[audioChoice.SelectedIndex].NoTrackDisplay,
								Encoder = DisplayConversions.DisplayAudioEncoder(encoder)
							};

							if (encoder != AudioEncoder.Ac3Passthrough && encoder != AudioEncoder.DtsPassthrough)
							{
								outputPreviewTrack.Bitrate = audioVM.Bitrate + " kbps";
							}

							this.AudioOutputPreviews.Add(outputPreviewTrack);
							outputTrackNumber++;
						}
					}
					else if (audioVM.TargetStreamIndex - 1 < chosenAudioTracks.Count)
					{
						int titleAudioIndex = chosenAudioTracks[audioVM.TargetStreamIndex - 1];

						var outputPreviewTrack = new AudioOutputPreview
						{
							TrackNumber = "#" + outputTrackNumber + ":",
							Name = this.SelectedTitle.AudioTracks[titleAudioIndex - 1].NoTrackDisplay,
							Encoder = DisplayConversions.DisplayAudioEncoder(encoder)
						};

						if (encoder != AudioEncoder.Ac3Passthrough && encoder != AudioEncoder.DtsPassthrough)
						{
							outputPreviewTrack.Bitrate = audioVM.Bitrate + " kbps";
						}

						this.AudioOutputPreviews.Add(outputPreviewTrack);
						outputTrackNumber++;
					}
				}

				this.NotifyPropertyChanged("HasAudioTracks");
			}
		}

		public void UpdateAudioEncodings()
		{
			this.profile.AudioEncodings = new List<AudioEncoding>();
			foreach (AudioEncodingViewModel audioEncodingVM in this.AudioEncodings)
			{
				this.profile.AudioEncodings.Add(audioEncodingVM.NewAudioEncoding);
			}
		}

		#endregion

		#region Advanced
		public AdvancedChoice ReferenceFrames
		{
			get
			{
				return this.referenceFrames;
			}

			set
			{
				this.referenceFrames = value;
				this.NotifyPropertyChanged("ReferenceFrames");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice BFrames
		{
			get
			{
				return this.bFrames;
			}

			set
			{
				this.bFrames = value;
				this.NotifyPropertyChanged("BFrames");
				this.NotifyPropertyChanged("BFramesOptionsVisible");
				this.NotifyPropertyChanged("PyramidalBFramesVisible");
				this.UpdateOptionsString();
			}
		}

		public bool BFramesOptionsVisible
		{
			get
			{
				return this.BFrames.Value != "0";
			}
		}

		public AdvancedChoice AdaptiveBFrames
		{
			get
			{
				return this.adaptiveBFrames;
			}

			set
			{
				this.adaptiveBFrames = value;
				this.NotifyPropertyChanged("AdaptiveBFrames");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice DirectPrediction
		{
			get
			{
				return this.directPrediction;
			}

			set
			{
				this.directPrediction = value;
				this.NotifyPropertyChanged("DirectPrediction");
				this.UpdateOptionsString();
			}
		}

		public bool WeightedPFrames
		{
			get
			{
				return this.weightedPFrames;
			}

			set
			{
				this.weightedPFrames = value;
				this.NotifyPropertyChanged("WeightedPFrames");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice PyramidalBFrames
		{
			get
			{
				return this.pyramidalBFrames;
			}

			set
			{
				this.pyramidalBFrames = value;
				this.NotifyPropertyChanged("PyramidalBFrames");
				this.UpdateOptionsString();
			}
		}

		public bool PyramidalBFramesVisible
		{
			get
			{
				return int.Parse(this.BFrames.Value) > 1;
			}
		}

		public AdvancedChoice MotionEstimationMethod
		{
			get
			{
				return this.motionEstimationMethod;
			}

			set
			{
				this.motionEstimationMethod = value;
				this.NotifyPropertyChanged("MotionEstimationMethod");
				this.NotifyPropertyChanged("MotionEstimationRangeVisible");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice SubpixelMotionEstimation
		{
			get
			{
				return this.subpixelMotionEstimation;
			}

			set
			{
				this.subpixelMotionEstimation = value;
				this.NotifyPropertyChanged("SubpixelMotionEstimation");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice MotionEstimationRange
		{
			get
			{
				return this.motionEstimationRange;
			}

			set
			{
				this.motionEstimationRange = value;
				this.NotifyPropertyChanged("MotionEstimationRange");
				this.UpdateOptionsString();
			}
		}

		public bool MotionEstimationRangeVisible
		{
			get
			{
				string motionMethod = this.MotionEstimationMethod.Value;
				return motionMethod == "umh" || motionMethod == "esa" || motionMethod == "tesa";
			}
		}

		public AdvancedChoice Analysis
		{
			get
			{
				return this.analysis;
			}

			set
			{
				this.analysis = value;
				this.NotifyPropertyChanged("Analysis");
				this.NotifyPropertyChanged("EightByEightDctVisible");
				this.UpdateOptionsString();
			}
		}

		public bool EightByEightDct
		{
			get
			{
				return this.eightByEightDct;
			}

			set
			{
				this.eightByEightDct = value;
				this.NotifyPropertyChanged("EightByEightDct");
				this.UpdateOptionsString();
			}
		}

		public bool EightByEightDctVisible
		{
			get
			{
				return this.Analysis.Value != "none";
			}
		}

		public bool CabacEntropyCoding
		{
			get
			{
				return this.cabacEntropyCoding;
			}

			set
			{
				this.cabacEntropyCoding = value;
				this.NotifyPropertyChanged("CabacEntropyCoding");
				this.NotifyPropertyChanged("PsychovisualTrellisVisible");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice Trellis
		{
			get
			{
				return this.trellis;
			}

			set
			{
				this.trellis = value;
				this.NotifyPropertyChanged("Trellis");
				this.NotifyPropertyChanged("PsychovisualTrellisVisible");
				this.UpdateOptionsString();
			}
		}

		public double AdaptiveQuantizationStrength
		{
			get
			{
				return this.adaptiveQuantizationStrength;
			}
			set
			{
				this.adaptiveQuantizationStrength = value;
				this.NotifyPropertyChanged("AdaptiveQuantizationStrength");
				this.UpdateOptionsString();
			}
		}

		public double PsychovisualRateDistortion
		{
			get
			{
				return this.psychovisualRateDistortion;
			}

			set
			{
				this.psychovisualRateDistortion = value;
				this.NotifyPropertyChanged("PsychovisualRateDistortion");
				this.UpdateOptionsString();
			}
		}

		public double PsychovisualTrellis
		{
			get
			{
				return this.psychovisualTrellis;
			}

			set
			{
				this.psychovisualTrellis = value;
				this.NotifyPropertyChanged("PsychovisualTrellis");
				this.UpdateOptionsString();
			}
		}

		public bool PsychovisualTrellisVisible
		{
			get
			{
				return this.CabacEntropyCoding && this.Trellis.Value != "0";
			}
		}

		public AdvancedChoice DeblockingStrength
		{
			get
			{
				return this.deblockingStrength;
			}

			set
			{
				this.deblockingStrength = value;
				this.NotifyPropertyChanged("DeblockingStrength");
				this.UpdateOptionsString();
			}
		}

		public AdvancedChoice DeblockingThreshold
		{
			get
			{
				return this.deblockingThreshold;
			}

			set
			{
				this.deblockingThreshold = value;
				this.NotifyPropertyChanged("DeblockingThreshold");
				this.UpdateOptionsString();
			}
		}

		public bool NoDctDecimate
		{
			get
			{
				return this.noDctDecimate;
			}

			set
			{
				this.noDctDecimate = value;
				this.NotifyPropertyChanged("NoDctDecimate");
				this.UpdateOptionsString();
			}
		}

		public string AdvancedOptionsString
		{
			get
			{
				return this.profile.X264Options;
			}

			set
			{
				this.profile.X264Options = value;
				this.UpdateUIFromAdvancedOptions();
				this.NotifyPropertyChanged("AdvancedOptionsString");
				this.IsModified = true;
			}
		}

		private void UpdateUIFromAdvancedOptions()
		{
			this.automaticChange = true;

			// Reset UI to defaults, and re-apply options.
			this.SetAdvancedToDefaults();

			if (this.profile.X264Options == null)
			{
				this.automaticChange = false;
				return;
			}

			// Check the updated options string. Update UI for any recognized options.
			string[] newOptionsSegments = this.profile.X264Options.Split(':');
			foreach (string newOptionsSegment in newOptionsSegments)
			{
				int equalsIndex = newOptionsSegment.IndexOf('=');
				if (equalsIndex >= 0)
				{
					string optionName = newOptionsSegment.Substring(0, equalsIndex);
					string optionValue = newOptionsSegment.Substring(equalsIndex + 1);

					if (optionName != string.Empty && optionValue != string.Empty)
					{
						AdvancedChoice newChoice;
						int parseInt;
						double parseDouble;
						string[] subParts;

						switch (optionName)
						{
							case "ref":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.ReferenceFrames.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.ReferenceFrames = newChoice;
									}
								}
								break;
							case "bframes":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.BFrames.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.BFrames = newChoice;
									}
								}
								break;
							case "b-adapt":
								newChoice = AdvancedChoices.AdaptiveBFrames.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.AdaptiveBFrames = newChoice;
								}
								break;
							case "direct":
								newChoice = AdvancedChoices.DirectPrediction.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.DirectPrediction = newChoice;
								}
								break;
							case "weightp":
								if (optionValue == "0")
								{
									this.WeightedPFrames = false;
								}
								else if (optionValue == "1")
								{
									this.WeightedPFrames = true;
								}
								break;
							case "b-pyramid":
								newChoice = AdvancedChoices.PyramidalBFrames.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.PyramidalBFrames = newChoice;
								}
								break;
							case "me":
								newChoice = AdvancedChoices.MotionEstimationMethod.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.MotionEstimationMethod = newChoice;
								}
								break;
							case "subme":
							case "subq":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.SubpixelMotionEstimation.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.SubpixelMotionEstimation = newChoice;
									}
								}
								break;
							case "merange":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.MotionEstimationRange.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.MotionEstimationRange = newChoice;
									}
								}
								break;
							case "analyse":
								newChoice = AdvancedChoices.Analysis.SingleOrDefault(choice => choice.Value == optionValue);
								if (newChoice != null)
								{
									this.Analysis = newChoice;
								}
								break;
							case "8x8dct":
								if (optionValue == "0")
								{
									this.EightByEightDct = false;
								}
								else if (optionValue == "1")
								{
									this.EightByEightDct = true;
								}
								break;
							case "cabac":
								if (optionValue == "0")
								{
									this.CabacEntropyCoding = false;
								}
								else if (optionValue == "1")
								{
									this.CabacEntropyCoding = true;
								}
								break;
							case "trellis":
								if (int.TryParse(optionValue, out parseInt))
								{
									newChoice = AdvancedChoices.Trellis.SingleOrDefault(choice => choice.Value == parseInt.ToString());
									if (newChoice != null)
									{
										this.Trellis = newChoice;
									}
								}
								break;
							case "aq-strength":
								if (double.TryParse(optionValue, out parseDouble) && parseDouble >= 0.0 && parseDouble <= 2.0)
								{
									this.AdaptiveQuantizationStrength = Math.Round(parseDouble, 1);
								}
								break;
							case "psy-rd":
								subParts = optionValue.Split(',');
								if (subParts.Length == 2)
								{
									double psyRD, psyTrellis;
									if (double.TryParse(subParts[0], out psyRD) && double.TryParse(subParts[1], out psyTrellis))
									{
										if (psyRD >= 0.0 && psyRD <= 1.0 && psyTrellis >= 0.0 && psyTrellis <= 1.0)
										{
											this.PsychovisualRateDistortion = Math.Round(psyRD, 1);
											this.PsychovisualTrellis = Math.Round(psyTrellis, 2);
										}
									}
								}
								break;
							case "no-dct-decimate":
								if (optionValue == "0")
								{
									this.NoDctDecimate = false;
								}
								else if (optionValue == "1")
								{
									this.NoDctDecimate = true;
								}
								break;
							case "deblock":
								subParts = optionValue.Split(',');
								if (subParts.Length == 2)
								{
									int dbStrength, dbThreshold;
									if (int.TryParse(subParts[0], out dbStrength) && int.TryParse(subParts[1], out dbThreshold))
									{
										newChoice = AdvancedChoices.DeblockingStrength.SingleOrDefault(choice => choice.Value == subParts[0]);
										if (newChoice != null)
										{
											this.DeblockingStrength = newChoice;
										}

										newChoice = AdvancedChoices.DeblockingThreshold.SingleOrDefault(choice => choice.Value == subParts[1]);
										if (newChoice != null)
										{
											this.DeblockingThreshold = newChoice;
										}
									}
								}
								break;
							default:
								break;
						}
					}
				}
			}

			this.automaticChange = false;
		}

		private void SetAdvancedToDefaults()
		{
			this.ReferenceFrames = AdvancedChoices.ReferenceFrames.SingleOrDefault(choice => choice.IsDefault);
			this.BFrames = AdvancedChoices.BFrames.SingleOrDefault(choice => choice.IsDefault);
			this.AdaptiveBFrames = AdvancedChoices.AdaptiveBFrames.SingleOrDefault(choice => choice.IsDefault);
			this.DirectPrediction = AdvancedChoices.DirectPrediction.SingleOrDefault(choice => choice.IsDefault);
			this.WeightedPFrames = true;
			this.PyramidalBFrames = AdvancedChoices.PyramidalBFrames.SingleOrDefault(choice => choice.IsDefault);
			this.MotionEstimationMethod = AdvancedChoices.MotionEstimationMethod.SingleOrDefault(choice => choice.IsDefault);
			this.SubpixelMotionEstimation = AdvancedChoices.SubpixelMotionEstimation.SingleOrDefault(choice => choice.IsDefault);
			this.MotionEstimationRange = AdvancedChoices.MotionEstimationRange.SingleOrDefault(choice => choice.IsDefault);
			this.Analysis = AdvancedChoices.Analysis.SingleOrDefault(choice => choice.IsDefault);
			this.EightByEightDct = true;
			this.CabacEntropyCoding = true;
			this.Trellis = AdvancedChoices.Trellis.SingleOrDefault(choice => choice.IsDefault);
			this.AdaptiveQuantizationStrength = 1.0;
			this.PsychovisualRateDistortion = 1.0;
			this.PsychovisualTrellis = 0.0;
			this.DeblockingStrength = AdvancedChoices.DeblockingStrength.SingleOrDefault(choice => choice.IsDefault);
			this.DeblockingThreshold = AdvancedChoices.DeblockingThreshold.SingleOrDefault(choice => choice.IsDefault);
			this.NoDctDecimate = false;
		}


		#endregion

		private int GetNearestValue(int number, int modulus)
		{
			int remainder = number % modulus;

			if (remainder == 0)
			{
				return number;
			}

			return remainder >= (modulus / 2) ? number + (modulus - remainder) : number - remainder;
		}

		private void RefreshOutputSize()
		{
			if (this.HasSourceData)
			{
				if (this.profile.Anamorphic == Anamorphic.None)
				{
					int width = this.profile.Width;
					if (width == 0)
					{
						width = this.mainViewModel.SelectedTitle.Resolution.Width;
					}

					if (this.profile.MaxWidth > 0 && width > profile.MaxWidth)
					{
						width = profile.MaxWidth;
					}

					int height = this.profile.Height;
					if (height == 0)
					{
						height = this.mainViewModel.SelectedTitle.Resolution.Height;
					}

					if (profile.MaxHeight > 0 && height > this.profile.MaxHeight)
					{
						height = this.profile.MaxHeight;
					}

					if (this.profile.KeepDisplayAspect)
					{
						if (this.profile.Width == 0 && this.profile.Height == 0 || this.profile.Width == 0)
						{
							width = (int)((double)height * this.SelectedTitle.AspectRatio);
						}
						else if (this.profile.Height == 0)
						{
							height = (int)((double)width / this.SelectedTitle.AspectRatio);
						}
					}

					this.OutputSourceResolution = width + " x " + height;
					this.OutputPixelAspectRatio = "1/1";
					this.OutputDisplayResolution = width + " x " + height;
				}
				else
				{
					EncodeJob job = this.mainViewModel.EncodeJob;
					job.EncodingProfile = this.profile;

					int width, height, parWidth, parHeight;
					this.mainViewModel.ScanInstance.GetAnamorphicSize(job, out width, out height, out parWidth, out parHeight);

					this.OutputSourceResolution = width + " x " + height;
					this.OutputPixelAspectRatio = parWidth + "/" + parHeight;
					this.OutputDisplayResolution = Math.Round(width * (((double)parWidth) / parHeight)) + " x " + height;
				}
			}
		}

		private void NotifyAllChanged()
		{
			this.automaticChange = true;

			this.NotifyPropertyChanged("WindowTitle");
			this.NotifyPropertyChanged("ProfileName");
			this.NotifyPropertyChanged("IsBuiltIn");
			this.NotifyPropertyChanged("DeleteButtonVisible");
			this.NotifyPropertyChanged("IsModified");
			this.NotifyPropertyChanged("OutputFormat");
			this.NotifyPropertyChanged("PreferredExtension");
			this.NotifyPropertyChanged("LargeFile");
			this.NotifyPropertyChanged("Optimize");
			this.NotifyPropertyChanged("IPod5GSupport");
			this.NotifyPropertyChanged("ShowMp4Choices");
			this.NotifyPropertyChanged("IncludeChapterMarkers");
			this.NotifyPropertyChanged("Width");
			this.NotifyPropertyChanged("WidthEnabled");
			this.NotifyPropertyChanged("Height");
			this.NotifyPropertyChanged("HeightEnabled");
			this.NotifyPropertyChanged("MaxWidth");
			this.NotifyPropertyChanged("MaxHeight");
			this.NotifyPropertyChanged("KeepDisplayAspect");
			this.NotifyPropertyChanged("KeepDisplayAspectEnabled");
			this.NotifyPropertyChanged("Anamorphic");
			this.NotifyPropertyChanged("CustomAnamorphicFieldsVisible");
			this.NotifyPropertyChanged("Modulus");
			this.NotifyPropertyChanged("UseDisplayWidth");
			this.NotifyPropertyChanged("DisplayWidth");
			this.NotifyPropertyChanged("PixelAspectX");
			this.NotifyPropertyChanged("PixelAspectY");
			this.NotifyPropertyChanged("CustomCropping");
			this.NotifyPropertyChanged("CropLeft");
			this.NotifyPropertyChanged("CropTop");
			this.NotifyPropertyChanged("CropRight");
			this.NotifyPropertyChanged("CropBottom");
			this.NotifyPropertyChanged("Detelecine");
			this.NotifyPropertyChanged("CustomDetelecine");
			this.NotifyPropertyChanged("CustomDetelecineVisible");
			this.NotifyPropertyChanged("Deinterlace");
			this.NotifyPropertyChanged("CustomDeinterlace");
			this.NotifyPropertyChanged("CustomDeinterlaceVisible");
			this.NotifyPropertyChanged("Decomb");
			this.NotifyPropertyChanged("CustomDecomb");
			this.NotifyPropertyChanged("CustomDecombVisible");
			this.NotifyPropertyChanged("Denoise");
			this.NotifyPropertyChanged("CustomDenoise");
			this.NotifyPropertyChanged("CustomDenoiseVisible");
			this.NotifyPropertyChanged("Deblock");
			this.NotifyPropertyChanged("DeblockText");
			this.NotifyPropertyChanged("Grayscale");
			this.NotifyPropertyChanged("SelectedEncoder");
			this.NotifyPropertyChanged("SelectedFramerate");
			this.NotifyPropertyChanged("PeakFramerate");
			this.NotifyPropertyChanged("PeakFramerateVisible");
			this.NotifyPropertyChanged("TwoPassEncoding");
			this.NotifyPropertyChanged("TurboFirstPass");
			this.NotifyPropertyChanged("TwoPassEncodingEnabled");
			this.NotifyPropertyChanged("TurboFirstPassEnabled");
			this.NotifyPropertyChanged("VideoEncodeRateType");
			this.NotifyPropertyChanged("TargetSize");
			this.NotifyPropertyChanged("VideoBitrate");
			this.NotifyPropertyChanged("Quality");
			this.NotifyPropertyChanged("QualitySliderMin");
			this.NotifyPropertyChanged("QualitySliderMax");
			this.NotifyPropertyChanged("QualitySliderLeftText");
			this.NotifyPropertyChanged("QualitySliderRightText");
			this.NotifyPropertyChanged("HasAudioTracks");
			this.NotifyPropertyChanged("AdvancedOptionsString");

			this.automaticChange = false;
		}

		private void PopulatePixelAspect()
		{
			if (this.SelectedTitle == null)
			{
				this.profile.PixelAspectX = 1;
				this.profile.PixelAspectY = 1;
			}
			else
			{
				this.profile.PixelAspectX = this.SelectedTitle.ParVal.Width;
				this.profile.PixelAspectY = this.SelectedTitle.ParVal.Height;
			}
		}

		private string CreateParDisplayString(int parWidth, int parHeight)
		{
			double pixelAspectRatio = ((double)parWidth) / parHeight;
			return pixelAspectRatio.ToString("F2") + " (" + parWidth + "/" + parHeight + ")";
		}

		private void OnMainPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// Refresh output and audio previews when selected title changes.
			if (e.PropertyName == "SelectedTitle")
			{
				this.RefreshOutputSize();
				this.RefreshAudioInput();
				this.NotifyPropertyChanged("HasSourceData");
				this.NotifyPropertyChanged("CropTop");
				this.NotifyPropertyChanged("CropBottom");
				this.NotifyPropertyChanged("CropLeft");
				this.NotifyPropertyChanged("CropRight");
				this.NotifyPropertyChanged("InputSourceResolution");
				this.NotifyPropertyChanged("InputPixelAspectRatio");
				this.NotifyPropertyChanged("InputDisplayResolution");
			}
		}

		/// <summary>
		/// Respondes to changed audio input.
		/// </summary>
		private void RefreshAudioInput()
		{
			this.RefreshAudioPreview();

			foreach (AudioEncodingViewModel encodingVM in this.AudioEncodings)
			{
				encodingVM.SetChosenTracks(this.mainViewModel.GetChosenAudioTracks(), this.mainViewModel.SelectedTitle);
			}
		}

		private void AudioChoicesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			this.RefreshAudioInput();
		}

		private void UpdatePreviewWindow()
		{
			PreviewViewModel previewVM = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;

			if (previewVM != null)
			{
				previewVM.RefreshPreviews();
			}
		}

		/// <summary>
		/// Update the x264 options string from a UI change.
		/// </summary>
		private void UpdateOptionsString()
		{
			if (this.automaticChange)
			{
				return;
			}

			List<string> newOptions = new List<string>();

			// First add any parts of the options string that don't correspond to the UI
			if (this.AdvancedOptionsString != null)
			{
				string[] existingSegments = this.AdvancedOptionsString.Split(':');
				foreach (string existingSegment in existingSegments)
				{
					string optionName = existingSegment;
					int equalsIndex = existingSegment.IndexOf('=');
					if (equalsIndex >= 0)
					{
						optionName = existingSegment.Substring(0, existingSegment.IndexOf("="));
					}

					if (!this.uiOptions.Contains(optionName) && optionName != string.Empty)
					{
						newOptions.Add(existingSegment);
					}
				}
			}

			// Now add everything from the UI
			if (!this.ReferenceFrames.IsDefault)
			{
				newOptions.Add("ref=" + this.ReferenceFrames.Value);
			}

			if (!this.BFrames.IsDefault)
			{
				newOptions.Add("bframes=" + this.BFrames.Value);
			}

			if (this.BFrames.Value != "0")
			{
				if (!this.AdaptiveBFrames.IsDefault)
				{
					newOptions.Add("b-adapt=" + this.AdaptiveBFrames.Value);
				}

				if (!this.DirectPrediction.IsDefault)
				{
					newOptions.Add("direct=" + this.DirectPrediction.Value);
				}

				if (!this.WeightedPFrames)
				{
					newOptions.Add("weightp=0");
				}

				if (this.BFrames.Value != "1" && !this.PyramidalBFrames.IsDefault)
				{
					newOptions.Add("b-pyramid=" + this.PyramidalBFrames.Value);
				}
			}

			if (!this.MotionEstimationMethod.IsDefault)
			{
				newOptions.Add("me=" + this.MotionEstimationMethod.Value);
			}

			if (!this.SubpixelMotionEstimation.IsDefault)
			{
				newOptions.Add("subme=" + this.SubpixelMotionEstimation.Value);
			}

			string motionEstimation = this.MotionEstimationMethod.Value;
			if ((motionEstimation == "umh" || motionEstimation == "esa" || motionEstimation == "tesa") && !this.MotionEstimationRange.IsDefault)
			{
				newOptions.Add("merange=" + this.MotionEstimationRange.Value);
			}

			if (!this.Analysis.IsDefault)
			{
				newOptions.Add("analyse=" + this.Analysis.Value);
			}

			if (this.Analysis.Value != "none" && !this.EightByEightDct)
			{
				newOptions.Add("8x8dct=0");
			}

			if (!this.CabacEntropyCoding)
			{
				newOptions.Add("cabac=0");
			}

			if (this.CabacEntropyCoding && !this.Trellis.IsDefault)
			{
				newOptions.Add("trellis=" + this.Trellis.Value);
			}

			double psTrellis = 0.0;
			if (this.CabacEntropyCoding && this.Trellis.Value != "0")
			{
				psTrellis = this.PsychovisualTrellis;
			}

			if (this.AdaptiveQuantizationStrength != 1.0)
			{
				newOptions.Add("aq-strength=" + this.AdaptiveQuantizationStrength.ToString("F1"));
			}

			if (this.PsychovisualRateDistortion < 1.0 || psTrellis > 0.0)
			{
				newOptions.Add("psy-rd=" + this.PsychovisualRateDistortion.ToString("F1") + "," + psTrellis.ToString("F2"));
			}

			if (this.NoDctDecimate)
			{
				newOptions.Add("no-dct-decimate=1");
			}

			if (!this.DeblockingStrength.IsDefault || !this.DeblockingThreshold.IsDefault)
			{
				newOptions.Add("deblock=" + this.DeblockingStrength.Value + "," + this.DeblockingThreshold.Value);
			}

			this.profile.X264Options = string.Join(":", newOptions);
			this.NotifyPropertyChanged("AdvancedOptionsString");
			this.IsModified = true;
		}
	}
}
