using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using HandBrake.Interop.SourceData;
using System.IO;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	using LocalResources;

	public class ChapterMarkersDialogViewModel : OkCancelDialogViewModel
	{
		private List<Chapter> chapters;

		private bool useDefaultNames;
		private ObservableCollection<ChapterNameViewModel> chapterNames;

		private ICommand importCsvFileCommand;

		public ChapterMarkersDialogViewModel(List<Chapter> chapters, List<string> currentNames, bool useDefaultNames)
		{
			this.chapters = chapters;

			this.chapterNames = new ObservableCollection<ChapterNameViewModel>();

			TimeSpan startTime = TimeSpan.Zero;
			for (int i = 0; i < this.chapters.Count; i++)
			{
				Chapter chapter = this.chapters[i];

				var viewModel = new ChapterNameViewModel { Number = i + 1, StartTime = startTime.ToString(Utilities.TimeFormat) };
				if (currentNames != null && i < currentNames.Count)
				{
					viewModel.Title = currentNames[i];
				}
				else
				{
					viewModel.Title = "Chapter " + (i + 1);
				}

				this.chapterNames.Add(viewModel);

				startTime += chapter.Duration;
			}

			this.useDefaultNames = useDefaultNames;
		}

		public bool UseDefaultNames
		{
			get
			{
				return this.useDefaultNames;
			}

			set
			{
				this.useDefaultNames = value;
				this.RaisePropertyChanged(() => this.UseDefaultNames);
			}
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

		public ICommand ImportCsvFileCommand
		{
			get
			{
				if (this.importCsvFileCommand == null)
				{
					this.importCsvFileCommand = new RelayCommand(() =>
					{
						string csvFile = FileService.Instance.GetFileNameLoad(
							Config.RememberPreviousFiles ? Config.LastCsvFolder : null, 
							"Import chapters file", 
							"csv", 
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
									string[] parts = line.Split(',');
									if (parts.Length == 2)
									{
										int number;
										if (int.TryParse(parts[0], out number) && !chapterMap.ContainsKey(number))
										{
											chapterMap.Add(number, parts[1]);
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
					});
				}

				return this.importCsvFileCommand;
			}
		}
	}
}
