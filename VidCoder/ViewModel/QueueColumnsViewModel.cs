using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using VidCoder.Properties;

namespace VidCoder.ViewModel
{
    public class QueueColumnsViewModel : OkCancelDialogViewModel
    {
        private ObservableCollection<ColumnViewModel> unusedColumns;
        private ObservableCollection<ColumnViewModel> usedColumns;

        private List<Tuple<string, double>> oldColumns;

        public QueueColumnsViewModel()
        {
            var unusedColumnKeys = new List<string>();
            foreach (string columnId in Utilities.DefaultQueueColumnSizes.Keys)
            {
                unusedColumnKeys.Add(columnId);
            }

            this.usedColumns = new ObservableCollection<ColumnViewModel>();

            this.oldColumns = Utilities.ParseQueueColumnList(Settings.Default.QueueColumns);
            foreach (Tuple<string, double> column in oldColumns)
            {
                this.usedColumns.Add(new ColumnViewModel(column.Item1));
                unusedColumnKeys.Remove(column.Item1);
            }

            this.unusedColumns = new ObservableCollection<ColumnViewModel>();
            foreach (string unusedColumnKey in unusedColumnKeys)
            {
                this.unusedColumns.Add(new ColumnViewModel(unusedColumnKey));
            }
        }

        public ObservableCollection<ColumnViewModel> UnusedColumns
        {
            get
            {
                return this.unusedColumns;
            }
        }

        public ObservableCollection<ColumnViewModel> UsedColumns
        {
            get
            {
                return this.usedColumns;
            }
        }

        public string NewColumns
        {
            get
            {
                StringBuilder newColumnsBuilder = new StringBuilder();
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
