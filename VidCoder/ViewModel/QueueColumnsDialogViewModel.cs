using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace VidCoder.ViewModel
{
	public class QueueColumnsDialogViewModel : OkCancelDialogViewModel
	{
		private List<Tuple<string, double>> oldColumns;

		public QueueColumnsDialogViewModel()
		{
			var unusedColumnKeys = new List<string>();
			foreach (string columnId in Utilities.DefaultQueueColumnSizes.Keys)
			{
				unusedColumnKeys.Add(columnId);
			}

			this.UsedColumns = new ObservableCollection<ColumnViewModel>();

			this.oldColumns = Utilities.ParseQueueColumnList(Config.QueueColumns);
			foreach (Tuple<string, double> column in oldColumns)
			{
				this.UsedColumns.Add(new ColumnViewModel(column.Item1));
				unusedColumnKeys.Remove(column.Item1);
			}

			this.UnusedColumns = new ObservableCollection<ColumnViewModel>();
			foreach (string unusedColumnKey in unusedColumnKeys)
			{
				this.UnusedColumns.Add(new ColumnViewModel(unusedColumnKey));
			}
		}

		public ObservableCollection<ColumnViewModel> UnusedColumns { get; }

		public ObservableCollection<ColumnViewModel> UsedColumns { get; }

		public string NewColumns
		{
			get
			{
				Dictionary<string, double> oldColumnSizeDict = new Dictionary<string, double>();
				foreach (Tuple<string, double> oldColumn in this.oldColumns)
				{
					oldColumnSizeDict.Add(oldColumn.Item1, oldColumn.Item2);
				}

				var columnPairs = new List<string>();
				foreach (ColumnViewModel newColumn in this.UsedColumns)
				{
					double columnSize;
					if (oldColumnSizeDict.ContainsKey(newColumn.Id))
					{
						columnSize = oldColumnSizeDict[newColumn.Id];
					}
					else
					{
						columnSize = Utilities.DefaultQueueColumnSizes[newColumn.Id];
					}

					columnPairs.Add(newColumn.Id + ":" + columnSize);
				}

				return string.Join("|", columnPairs);
			}
		}
	}
}
