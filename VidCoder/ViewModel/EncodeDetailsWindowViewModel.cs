using System;
using System.Diagnostics;
using System.Reactive.Linq;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Resources;
using VidCoder.Services;

namespace VidCoder.ViewModel
{
	public class EncodeDetailsWindowViewModel : ReactiveObject
	{
		public EncodeDetailsWindowViewModel()
		{
			this.ProcessingService.WhenAnyValue(x => x.TaskNumber, x=> x.TotalTasks, (taskNumber, totalTasks) =>
			{
				return taskNumber + "/" + totalTasks;
			}).ToProperty(this, x => x.TaskNumberDisplay, out this.taskNumberDisplay);

			this.ProcessingService.WhenAnyValue(x => x.OverallEncodeTime).Select(overallEncodeTime =>
			{
				return Utilities.FormatTimeSpan(overallEncodeTime);
			}).ToProperty(this, x => x.OverallEncodeTime, out this.overallEncodeTime);

			this.ProcessingService.WhenAnyValue(x => x.OverallEta).Select(overallEta =>
			{
				return Utilities.FormatTimeSpan(overallEta);
			}).ToProperty(this, x => x.OverallEta, out this.overallEta);
		}

		public ProcessingService ProcessingService { get; } = Ioc.Get<ProcessingService>();

		private readonly ObservableAsPropertyHelper<string> taskNumberDisplay;
		public string TaskNumberDisplay => this.taskNumberDisplay.Value;

		private readonly ObservableAsPropertyHelper<string> overallEncodeTime;
		public string OverallEncodeTime => this.overallEncodeTime.Value;

		private ObservableAsPropertyHelper<string> overallEta;
		public string OverallEta => this.overallEta.Value;
	}
}
