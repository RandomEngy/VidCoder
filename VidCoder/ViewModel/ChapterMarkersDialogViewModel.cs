using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Windows.Input;
using HandBrake.Interop.Interop.Json.Scan;
using VidCoder.Services;
using VidCoderCommon.Extensions;
using ReactiveUI;
using VidCoder.Resources;
using VidCoder.Extensions;

namespace VidCoder.ViewModel;

public class ChapterMarkersDialogViewModel : OkCancelDialogViewModel
{
	private List<SourceChapter> chapters;

	private ObservableCollection<ChapterNameViewModel> chapterNames;

	public ChapterMarkersDialogViewModel(List<SourceChapter> chapters, List<string> currentNames, bool useDefaultNames)
	{
		this.chapters = chapters;

		this.chapterNames = new ObservableCollection<ChapterNameViewModel>();

		TimeSpan startTime = TimeSpan.Zero;
		for (int i = 0; i < this.chapters.Count; i++)
		{
			SourceChapter chapter = this.chapters[i];

			var viewModel = new ChapterNameViewModel { Number = i + 1, StartTime = startTime.FormatWithHours() };
			if (currentNames != null && i < currentNames.Count)
			{
				viewModel.Title = currentNames[i];
			}
			else if (!string.IsNullOrWhiteSpace(chapter.Name))
			{
				viewModel.Title = chapter.Name;
			}
			else
			{
				viewModel.Title = string.Format(
					CultureInfo.CurrentCulture,
					EncodingRes.DefaultChapterName,
					i + 1);
			}

			this.chapterNames.Add(viewModel);

			startTime += chapter.Duration.ToSpan();
		}

		this.useDefaultNames = useDefaultNames;
	}

	private bool useDefaultNames;
	public bool UseDefaultNames
	{
		get { return this.useDefaultNames; }
		set { this.RaiseAndSetIfChanged(ref this.useDefaultNames, value); }
	}

	public ObservableCollection<ChapterNameViewModel> ChapterNames
	{
		get
		{
			return this.chapterNames;
		}
	}

	public List<string> ChapterNamesList
	{
		get
		{
			var result = new List<string>();
			foreach (ChapterNameViewModel chapterVM in this.ChapterNames)
			{
				result.Add(chapterVM.Title);
			}

			return result;
		}
	}

	private ReactiveCommand<Unit, Unit> importCsvFile;
	public ICommand ImportCsvFile
	{
		get
		{
			return this.importCsvFile ?? (this.importCsvFile = ReactiveCommand.Create(() =>
			{
				string csvFile = FileService.Instance.GetFileNameLoad(
					Config.RememberPreviousFiles ? Config.LastCsvFolder : null,
					"Import chapters file",
					"CSV Files|*.csv");
				if (csvFile != null)
				{
					if (Config.RememberPreviousFiles)
					{
						Config.LastCsvFolder = Path.GetDirectoryName(csvFile);
					}

					bool success = false;
					var chapterMap = new Dictionary<int, string>();

					try
					{
						string[] lines = File.ReadAllLines(csvFile);

						foreach (string line in lines)
						{
							int commaIndex = line.IndexOf(',');
							if (commaIndex > 0)
							{
								int number;
								if (int.TryParse(line.Substring(0, commaIndex), out number) && !chapterMap.ContainsKey(number))
								{
									chapterMap.Add(number, line.Substring(commaIndex + 1));
								}
							}
						}

						success = true;
					}
					catch (IOException)
					{
						Utilities.MessageBox.Show(ChapterMarkersRes.CouldNotReadFileMessage);
					}

					if (success)
					{
						for (int i = 0; i < this.chapters.Count; i++)
						{
							if (chapterMap.ContainsKey(i + 1))
							{
								this.chapterNames[i].Title = chapterMap[i + 1];
							}
						}

						this.UseDefaultNames = false;
					}
				}
			}));
		}
	}
}
