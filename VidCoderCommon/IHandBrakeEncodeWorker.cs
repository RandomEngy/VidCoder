using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VidCoderCommon.Model;
using VidCoderCommon.Model.Job;

namespace VidCoderCommon;

public interface IHandBrakeEncodeWorker : IHandBrakeWorker
{
	void StartEncode(
		VCJob job,
		int previewNumber,
		int previewSeconds,
		string defaultChapterNameFormat,
		JobConfiguration jobConfiguration,
		int qsvGpu);

	/// <summary>
	/// Starts an encode with the given encode JSON.
	/// </summary>
	/// <param name="encodeJson">The encode JSON.</param>
	void StartEncodeFromJson(string encodeJson);

	void PauseEncode();

	void ResumeEncode();

	void StopEncode();
}
