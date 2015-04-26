using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using System.Collections.ObjectModel;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using VidCoder.Extensions;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.Model.Encoding;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	using System.Data.SQLite;
	using Resources;
	using Services;

	public class QueueTitlesWindowViewModel : OkCancelDialogViewModel
	{
		private ObservableCollection<TitleSelectionViewModel> titles;
		private ObservableCollection<TitleSelectionViewModel> selectedTitles;
		private bool titleStartOverrideEnabled;
		private int titleStartOverride;

		private MainViewModel main;

		public QueueTitlesWindowViewModel()
		{
			this.main = Ioc.Container.GetInstance<MainViewModel>();
			this.PickersVM = Ioc.Container.GetInstance<PickersViewModel>();
			this.WindowManagerVM = Ioc.Container.GetInstance<WindowManagerViewModel>();

			this.selectedTitles = new ObservableCollection<TitleSelectionViewModel>();
			this.titleStartOverrideEnabled = Config.QueueTitlesUseTitleOverride;
			this.titleStartOverride = Config.QueueTitlesTitleOverride;
			this.nameOverrideEnabled = Config.QueueTitlesUseNameOverride;
			this.nameOverride = Config.QueueTitlesNameOverride;

			this.titles = new ObservableCollection<TitleSelectionViewModel>();
			this.RefreshTitles();

			Messenger.Default.Register<VideoSourceChangedMessage>(
				this,
				message =>
				{
					this.RefreshTitles();
				});

			Messenger.Default.Register<TitleRangeSelectChangedMessage>(
				this,
				message =>
				{
					this.SetSelectedFromRange();
				});

			Messenger.Default.Register<PickerChangedMessage>(
				this,
				message =>
				{
					this.SetSelectedFromRange();
				});

			this.selectedTitles.CollectionChanged +=
				(sender, args) =>
			    {
					this.RaisePropertyChanged(() => this.TitleDetailsVisible);

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

						this.PreviewImage = this.main.ScanInstance.GetPreview(previewProfile.CreatePreviewSettings(title), 2);
						this.RaisePropertyChanged(() => this.TitleText);
					}
			    };
		}

		public PickersViewModel PickersVM { get; private set; }

		public WindowManagerViewModel WindowManagerVM { get; private set; }

		public ObservableCollection<TitleSelectionViewModel> Titles
		{
			get
			{
				return this.titles;
			}
		}

		public ObservableCollection<TitleSelectionViewModel> SelectedTitles
		{
			get
			{
				return this.selectedTitles;
			}
		}

		public bool TitleDetailsVisible
		{
			get
			{
				return this.selectedTitles.Count == 1;
			}
		}

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
			get
			{
				return this.previewImage;
			}

			set
			{
				this.previewImage = value;
				this.RaisePropertyChanged(() => this.PreviewImage);
			}
		}

		public bool PlayAvailable
		{
			get
			{
				return Utilities.IsDvdFolder(this.main.SourcePath);
			}
		}

		public bool TitleStartOverrideEnabled
		{
			get
			{
				return this.titleStartOverrideEnabled;
			}

			set
			{
				this.titleStartOverrideEnabled = value;
				this.RaisePropertyChanged(() => this.TitleStartOverrideEnabled);
			}
		}

		public int TitleStartOverride
		{
			get
			{
				return this.titleStartOverride;
			}

			set
			{
				this.titleStartOverride = value;
				this.RaisePropertyChanged(() => this.TitleStartOverride);
			}
		}

		private bool nameOverrideEnabled;
		public bool NameOverrideEnabled
		{
			get
			{
				return this.nameOverrideEnabled;
			}

			set
			{
				this.nameOverrideEnabled = value;
				this.RaisePropertyChanged(() => this.NameOverrideEnabled);
			}
		}

		private string nameOverride;
		public string NameOverride
		{
			get
			{
				return this.nameOverride;
			}

			set
			{
				this.nameOverride = value;
				this.RaisePropertyChanged(() => this.NameOverride);
			}
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

		private RelayCommand playCommand;
		public RelayCommand PlayCommand
		{
			get
			{
				return this.playCommand ?? (this.playCommand = new RelayCommand(() =>
					{
						IVideoPlayer player = Players.Installed.FirstOrDefault(p => p.Id == Config.PreferredPlayer);
						if (player == null)
						{
							player = Players.Installed[0];
						}

						player.PlayTitle(this.main.SourcePath, this.selectedTitles[0].Title.Index);
					},
					() =>
					{
						return Players.Installed.Count > 0;
					}));
			}
		}

		private RelayCommand addToQueueCommand;
		public RelayCommand AddToQueueCommand
		{
			get
			{
				return this.addToQueueCommand ?? (this.addToQueueCommand = new RelayCommand(() =>
				{
					this.DialogResult = true;

					var processingVM = Ioc.Container.GetInstance<ProcessingViewModel>();
					processingVM.QueueTitles(
						this.CheckedTitles, 
						this.TitleStartOverrideEnabled ? this.TitleStartOverride : -1,
						this.NameOverrideEnabled ? this.NameOverride : null);

					WindowManager.Close(this);
					this.OnClosing();
				}, () =>
				{
					return this.CanClose;
				}));
			}
		}

		public override void OnClosing()
		{
			using (SQLiteTransaction transaction = Database.ThreadLocalConnection.BeginTransaction())
			{
				Config.QueueTitlesUseTitleOverride = this.TitleStartOverrideEnabled;
				Config.QueueTitlesTitleOverride = this.TitleStartOverride;
				Config.QueueTitlesUseNameOverride = this.NameOverrideEnabled;
				Config.QueueTitlesNameOverride = this.NameOverride;

				transaction.Commit();
			}

			base.OnClosing();
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
			Picker picker = this.PickersVM.SelectedPicker.Picker;

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
