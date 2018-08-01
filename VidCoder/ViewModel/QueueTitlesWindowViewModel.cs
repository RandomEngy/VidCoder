using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Media;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;
using ReactiveUI;

namespace VidCoder.ViewModel
{
	public class QueueTitlesWindowViewModel : OkCancelDialogViewModel
	{
		private ReactiveList<TitleSelectionViewModel> titles;
		private ReactiveList<TitleSelectionViewModel> selectedTitles;
		private IWindowManager windowManager;

		private MainViewModel main;

		public QueueTitlesWindowViewModel()
		{
			this.main = Ioc.Get<MainViewModel>();
			this.PickersService = Ioc.Get<PickersService>();
			this.windowManager = Ioc.Get<IWindowManager>();

			this.selectedTitles = new ReactiveList<TitleSelectionViewModel>();
			this.titleStartOverrideEnabled = Config.QueueTitlesUseTitleOverride;
			this.titleStartOverride = Config.QueueTitlesTitleOverride;
			this.nameOverrideEnabled = Config.QueueTitlesUseNameOverride;
			this.nameOverride = Config.QueueTitlesNameOverride;

			this.titles = new ReactiveList<TitleSelectionViewModel>();
			this.RefreshTitles();

			this.main.WhenAnyValue(x => x.SourceData)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.RefreshTitles();
				});

			this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.TitleRangeSelectEnabled)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.SetSelectedFromRange();
				});

			this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.TitleRangeSelectStartMinutes)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.SetSelectedFromRange();
				});

			this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.TitleRangeSelectEndMinutes)
				.Skip(1)
				.Subscribe(_ =>
				{
					this.SetSelectedFromRange();
				});

			this.selectedTitles.CountChanged
				.Select(count => count == 1)
				.ToProperty(this, x => x.TitleDetailsVisible, out this.titleDetailsVisible, initialValue: false);

			this.selectedTitles.CollectionChanged +=
				(sender, args) =>
			    {
					if (this.selectedTitles.Count == 1)
					{
						SourceTitle title = this.selectedTitles[0].Title;

						// Do preview
						var previewProfile =
							new VCProfile
							{
								CustomCropping = true,
								Cropping = new VCCropping(),
								VideoEncoder = "x264",
								AudioEncodings = new List<AudioEncoding>()
							};

						var previewJob =
							new VCJob
							{
								RangeType = VideoRangeType.All,
								Title = title.Index,
								EncodingProfile = previewProfile
							};

						this.PreviewImage = BitmapUtilities.ConvertToBitmapImage(BitmapUtilities.ConvertByteArrayToBitmap(this.main.ScanInstance.GetPreview(previewProfile.CreatePreviewSettings(title), 2, deinterlace: false)));
						this.RaisePropertyChanged(nameof(this.TitleText));
					}
			    };
		}

		public PickersService PickersService { get; }

		public ReactiveList<TitleSelectionViewModel> Titles
		{
			get
			{
				return this.titles;
			}
		}

		public ReactiveList<TitleSelectionViewModel> SelectedTitles
		{
			get
			{
				return this.selectedTitles;
			}
		}

		private ObservableAsPropertyHelper<bool> titleDetailsVisible;
		public bool TitleDetailsVisible => this.titleDetailsVisible.Value;

		public string TitleText
		{
			get
			{
				if (!this.TitleDetailsVisible)
				{
					return string.Empty;
				}

				return string.Format(QueueTitlesRes.TitleFormat, this.selectedTitles[0].Title.Index);
			}
		}

		private ImageSource previewImage;
		public ImageSource PreviewImage
		{
			get { return this.previewImage; }
			set { this.RaiseAndSetIfChanged(ref this.previewImage, value); }
		}

		public bool PlayAvailable
		{
			get
			{
				return !Utilities.IsRunningAsAppx && Utilities.IsDvdFolder(this.main.SourcePath);
			}
		}

		private bool titleStartOverrideEnabled;
		public bool TitleStartOverrideEnabled
		{
			get { return this.titleStartOverrideEnabled; }
			set { this.RaiseAndSetIfChanged(ref this.titleStartOverrideEnabled, value); }
		}

		private int titleStartOverride;
		public int TitleStartOverride
		{
			get { return this.titleStartOverride; }
			set { this.RaiseAndSetIfChanged(ref this.titleStartOverride, value); }
		}

		private bool nameOverrideEnabled;
		public bool NameOverrideEnabled
		{
			get { return this.nameOverrideEnabled; }
			set { this.RaiseAndSetIfChanged(ref this.nameOverrideEnabled, value); }
		}

		private string nameOverride;
		public string NameOverride
		{
			get { return this.nameOverride; }
			set { this.RaiseAndSetIfChanged(ref this.nameOverride, value); }
		}

		public List<SourceTitle> CheckedTitles
		{
			get
			{
				List<SourceTitle> checkedTitles = new List<SourceTitle>();
				foreach (TitleSelectionViewModel titleVM in this.titles)
				{
					if (titleVM.Selected)
					{
						checkedTitles.Add(titleVM.Title);
					}
				}

				return checkedTitles;
			}
		}

		private ReactiveCommand play;
		public ReactiveCommand Play
		{
			get
			{
				return this.play ?? (this.play = ReactiveCommand.Create(
					() =>
					{
						IVideoPlayer player = Players.Installed.FirstOrDefault(p => p.Id == Config.PreferredPlayer);
						if (player == null)
						{
							player = Players.Installed[0];
						}

						player.PlayTitle(this.main.SourcePath, this.selectedTitles[0].Title.Index);
					},
					MvvmUtilities.CreateConstantObservable(!Utilities.IsRunningAsAppx && Players.Installed.Count > 0)));
			}
		}

		private ReactiveCommand addToQueue;
		public ReactiveCommand AddToQueue
		{
			get
			{
				return this.addToQueue ?? (this.addToQueue = ReactiveCommand.Create(() =>
				{
					this.DialogResult = true;

					string nameOverrideLocal;
					if (this.NameOverrideEnabled)
					{
						nameOverrideLocal = this.NameOverride;
					}
					else
					{
						var picker = this.PickersService.SelectedPicker.Picker;
						if (picker.NameFormatOverrideEnabled)
						{
							nameOverrideLocal = picker.NameFormatOverride;
						}
						else
						{
							nameOverrideLocal = null;
						}
					}

					var processingService = Ioc.Get<ProcessingService>();
					processingService.QueueTitles(
						this.CheckedTitles,
						this.TitleStartOverrideEnabled ? this.TitleStartOverride : -1,
						nameOverrideLocal);

					this.windowManager.Close(this);
				}));
			}
		}


		public override bool OnClosing()
		{
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.QueueTitlesUseTitleOverride = this.TitleStartOverrideEnabled;
				Config.QueueTitlesTitleOverride = this.TitleStartOverride;
				Config.QueueTitlesUseNameOverride = this.NameOverrideEnabled;
				Config.QueueTitlesNameOverride = this.NameOverride;

				transaction.Commit();
			}

			return base.OnClosing();
		}

		public void HandleCheckChanged(TitleSelectionViewModel changedTitleVM, bool newValue)
		{
			if (this.SelectedTitles.Contains(changedTitleVM))
			{
				foreach (TitleSelectionViewModel titleVM in this.SelectedTitles)
				{
					if (titleVM != changedTitleVM && titleVM.Selected != newValue)
					{
						titleVM.SetSelected(newValue);
					}
				}
			}
		}

		private void RefreshTitles()
		{
			this.titles.Clear();

			if (this.main.SourceData != null)
			{
				foreach (SourceTitle title in this.main.SourceData.Titles)
				{
					var titleVM = new TitleSelectionViewModel(title, this);
					this.titles.Add(titleVM);
				}

				// Perform range selection if enabled.
				this.SetSelectedFromRange();
			}
		}

		private void SetSelectedFromRange()
		{
			Picker picker = this.PickersService.SelectedPicker.Picker;

			if (picker.TitleRangeSelectEnabled)
			{
				TimeSpan lowerBound = TimeSpan.FromMinutes(picker.TitleRangeSelectStartMinutes);
				TimeSpan upperBound = TimeSpan.FromMinutes(picker.TitleRangeSelectEndMinutes);

				foreach (TitleSelectionViewModel titleVM in this.titles)
				{
					TimeSpan titleDuration = titleVM.Title.Duration.ToSpan();
					if (titleDuration >= lowerBound && titleDuration <= upperBound)
					{
						if (!titleVM.Selected)
						{
							titleVM.SetSelected(true);
						}
					}
					else if (titleVM.Selected)
					{
						titleVM.SetSelected(false);
					}
				}
			}
			else
			{
				foreach (TitleSelectionViewModel titleVM in this.titles)
				{
					if (titleVM.Selected)
					{
						titleVM.SetSelected(false);
					}
				}
			}
		}
	}
}
