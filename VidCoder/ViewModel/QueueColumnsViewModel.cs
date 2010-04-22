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
        private ObservableCollection<string> unusedColumns;
        private ObservableCollection<string> usedColumns;

        private List<Tuple<string, double>> oldColumns;

        public QueueColumnsViewModel()
        {
            this.unusedColumns = new ObservableCollection<string>();
            foreach (string columnId in Utilities.DefaultQueueColumnSizes.Keys)
            {
                this.unusedColumns.Add(columnId);
            }

            this.usedColumns = new ObservableCollection<string>();

            this.oldColumns = Utilities.ParseSizeList(Settings.Default.QueueColumns);
            foreach (Tuple<string, double> column in oldColumns)
            {
                this.usedColumns.Add(column.Item1);
                this.unusedColumns.Remove(column.Item1);
            }
        }

        public ObservableCollection<string> UnusedColumns
        {
            get
            {
                return this.unusedColumns;
            }
        }

        public ObservableCollection<string> UsedColumns
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
                foreach (string newColumn in this.UsedColumns)
                {
                    double columnSize;
                    if (oldColumnSizeDict.ContainsKey(newColumn))
                    {
                        columnSize = oldColumnSizeDict[newColumn];
                    }
                    else
                    {
                        columnSize = Utilities.DefaultQueueColumnSizes[newColumn];
                    }

                    columnPairs.Add(newColumn + ":" + columnSize);
                }

                return string.Join("|", columnPairs);
            }
        }
    }
}
