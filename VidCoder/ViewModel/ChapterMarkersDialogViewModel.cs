using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Input;
using VidCoder.Properties;
using System.IO;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class ChapterMarkersDialogViewModel : OkCancelDialogViewModel
	{
		private int chapters;

		private bool useDefaultNames;
		private ObservableCollection<ChapterNameViewModel> chapterNames;

		private ICommand importCsvFileCommand;

		public ChapterMarkersDialogViewModel(int chapters, List<string> currentNames, bool useDefaultNames)
		{
			this.chapters = chapters;

			this.chapterNames = new ObservableCollection<ChapterNameViewModel>();
			for (int i = 0; i < chapters; i++)
			{
				if (currentNames != null && i < currentNames.Count)
				{
					this.chapterNames.Add(new ChapterNameViewModel { Number = i + 1, Title = currentNames[i] });
				}
				else
				{
					this.chapterNames.Add(new ChapterNameViewModel { Number = i + 1, Title = "Chapter " + (i + 1) });
				}
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
				this.NotifyPropertyChanged("UseDefaultNames");
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
					this.importCsvFileCommand = new RelayCommand(param =>
					{
						string csvFile = FileService.Instance.GetFileNameLoad("csv", "CSV Files|*.csv", Settings.Default.LastCsvFolder);
						if (csvFile != null)
						{
							Settings.Default.LastCsvFolder = Path.GetDirectoryName(csvFile);
							Settings.Default.Save();

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
								ServiceFactory.MessageBoxService.Show("Could not read file.");
							}

							if (success)
							{
								for (int i = 0; i < this.chapters; i++)
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
