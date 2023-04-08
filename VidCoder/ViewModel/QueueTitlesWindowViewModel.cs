using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Media;
using HandBrake.Interop.Interop.Json.Scan;
using Microsoft.AnyContainer;
using VidCoder.Extensions;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;
using VidCoder.Services.Windows;
using VidCoderCommon.Extensions;
using VidCoderCommon.Model;
using ReactiveUI;
using HandBrake.Interop.Interop.Json.Encode;

namespace VidCoder.ViewModel;

public class QueueTitlesWindowViewModel : OkCancelDialogViewModel
{
	private IWindowManager windowManager;

	private MainViewModel main;

	private List<IDisposable> subscriptions = new List<IDisposable>();

	public QueueTitlesWindowViewModel()
	{
		this.main = StaticResolver.Resolve<MainViewModel>();
		this.PickersService = StaticResolver.Resolve<PickersService>();
		this.windowManager = StaticResolver.Resolve<IWindowManager>();

		this.titleStartOverrideEnabled = Config.QueueTitlesUseTitleOverride;
		this.titleStartOverride = Config.QueueTitlesTitleOverride;
		this.nameOverrideEnabled = Config.QueueTitlesUseNameOverride;
		this.nameOverride = Config.QueueTitlesNameOverride;

		this.RefreshTitles();

		this.subscriptions.Add(this.main.WhenAnyValue(x => x.SourceData)
			.Skip(1)
			.Subscribe(_ =>
			{
				this.RefreshTitles();
			}));

		this.subscriptions.Add(this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.TitleRangeSelectEnabled)
			.Skip(1)
			.Subscribe(_ =>
			{
				this.SetSelectedFromRange();
			}));

		this.subscriptions.Add(this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.TitleRangeSelectStartMinutes)
			.Skip(1)
			.Subscribe(_ =>
			{
				this.SetSelectedFromRange();
			}));

		this.subscriptions.Add(this.PickersService.WhenAnyValue(x => x.SelectedPicker.Picker.TitleRangeSelectEndMinutes)
			.Skip(1)
			.Subscribe(_ =>
			{
				this.SetSelectedFromRange();
			}));

		this.SelectedTitles.CollectionChanged += this.OnSelectedTitlesCollectionChanged;
	}

	private void OnSelectedTitlesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(this.TitleDetailsVisible));

		if (this.SelectedTitles.Count == 1 && this.main.SourceData != null)
		{
			SourceTitle title = this.SelectedTitles[0].Title;

			// Do preview
			VCJob job = this.main.EncodeJob;
			job.Title = title.Index;
			JsonEncodeFactory factory = new JsonEncodeFactory(new StubLogger());

			JsonEncodeObject jsonEncodeObject = factory.CreateJsonObject(
				job,
				title,
				EncodingRes.DefaultChapterName,
				Config.EnableNVDec);

			this.PreviewImage = BitmapUtilities.ConvertToBitmapImage(BitmapUtilities.ConvertByteArrayToBitmap(this.main.ScanInstance.GetPreview(jsonEncodeObject, 2)));
			this.RaisePropertyChanged(nameof(this.TitleText));
		}
	}

	public PickersService PickersService { get; }

	public ObservableCollection<TitleSelectionViewModel> Titles { get; } = new ObservableCollection<TitleSelectionViewModel>();

	public ObservableCollection<TitleSelectionViewModel> SelectedTitles { get; } = new ObservableCollection<TitleSelectionViewModel>();

	public bool TitleDetailsVisible
	{
		get
		{
			return this.SelectedTitles.Count == 1;
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

			return string.Format(QueueTitlesRes.TitleFormat, this.SelectedTitles[0].Title.Index);
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
			return Utilities.IsDvdFolder(this.main.SourcePath);
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
			foreach (TitleSelectionViewModel titleVM in this.Titles)
			{
				if (titleVM.Selected)
				{
					checkedTitles.Add(titleVM.Title);
				}
			}

			return checkedTitles;
		}
	}

	private ReactiveCommand<Unit, Unit> play;
	public ICommand Play
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

					player.PlayTitle(this.main.SourcePath, this.SelectedTitles[0].Title.Index);
				},
				MvvmUtilities.CreateConstantObservable(Players.Installed.Count > 0)));
		}
	}

	private ReactiveCommand<Unit, Unit> addToQueue;
	public ICommand AddToQueue
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
					if (picker.UseCustomFileNameFormat)
					{
						nameOverrideLocal = picker.OutputFileNameFormat;
					}
					else
					{
						nameOverrideLocal = null;
					}
				}

				var processingService = StaticResolver.Resolve<ProcessingService>();
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
		using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
		{
			Config.QueueTitlesUseTitleOverride = this.TitleStartOverrideEnabled;
			Config.QueueTitlesTitleOverride = this.TitleStartOverride;
			Config.QueueTitlesUseNameOverride = this.NameOverrideEnabled;
			Config.QueueTitlesNameOverride = this.NameOverride;

			transaction.Commit();
		}

		foreach (IDisposable disposable in this.subscriptions)
		{
			disposable.Dispose();
		}

		this.SelectedTitles.CollectionChanged -= this.OnSelectedTitlesCollectionChanged;

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
		this.Titles.Clear();

		if (this.main.SourceData != null)
		{
			foreach (SourceTitle title in this.main.SourceData.Titles)
			{
				var titleVM = new TitleSelectionViewModel(title, this);
				this.Titles.Add(titleVM);
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

			foreach (TitleSelectionViewModel titleVM in this.Titles)
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
			foreach (TitleSelectionViewModel titleVM in this.Titles)
			{
				if (titleVM.Selected)
				{
					titleVM.SetSelected(false);
				}
			}
		}
	}
}
