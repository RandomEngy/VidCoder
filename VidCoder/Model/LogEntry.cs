namespace VidCoder.Model
{
	public class LogEntry
	{
		public LogType LogType { get; set; }
		public LogSource Source { get; set; }
		public string Text { get; set; }
	}
}
