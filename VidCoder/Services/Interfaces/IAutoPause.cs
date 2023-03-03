using System;
namespace VidCoder.Services;

public interface IAutoPause
{
	event EventHandler PauseEncoding;
	void ReportPause();
	void ReportResume();
	void ReportStart();
	void ReportStop();
	event EventHandler ResumeEncoding;
}
