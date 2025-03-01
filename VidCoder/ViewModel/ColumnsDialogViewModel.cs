using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace VidCoder.ViewModel;

public class ColumnsDialogViewModel : OkCancelDialogViewModel
{
	private readonly Dictionary<string, double> defaultColumnSizes;
	private List<(string columnId, double width)> oldColumns;

	public ColumnsDialogViewModel(string columnsString, Dictionary<string, double> defaultColumnSizes, string type, string title)
	{
		this.defaultColumnSizes = defaultColumnSizes;
		Title = title;
		var unusedColumnKeys = new List<string>();
		foreach (string columnId in defaultColumnSizes.Keys)
		{
			unusedColumnKeys.Add(columnId);
		}

		this.UsedColumns = new ObservableCollection<ColumnViewModel>();

		this.oldColumns = Utilities.ParseColumnList(columnsString, defaultColumnSizes);
		foreach ((string columnId, double width) column in oldColumns)
		{
			this.UsedColumns.Add(new ColumnViewModel(column.columnId, type));
			unusedColumnKeys.Remove(column.columnId);
		}

		this.UnusedColumns = new ObservableCollection<ColumnViewModel>();
		foreach (string unusedColumnKey in unusedColumnKeys)
		{
			this.UnusedColumns.Add(new ColumnViewModel(unusedColumnKey, type));
		}

	}

	public string Title { get; }

	public ObservableCollection<ColumnViewModel> UnusedColumns { get; }

	public ObservableCollection<ColumnViewModel> UsedColumns { get; }

	public string NewColumns
	{
		get
		{
			Dictionary<string, double> oldColumnSizeDict = new Dictionary<string, double>();
			foreach ((string columnId, double width) oldColumn in this.oldColumns)
			{
				oldColumnSizeDict.Add(oldColumn.columnId, oldColumn.width);
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
					columnSize = this.defaultColumnSizes[newColumn.Id];
				}

				columnPairs.Add(newColumn.Id + ":" + columnSize);
			}

			return string.Join("|", columnPairs);
		}
	}
}
