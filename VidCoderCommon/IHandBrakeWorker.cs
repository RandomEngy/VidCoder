using System.Threading;
using System.Threading.Tasks;
using VidCoderCommon.Model;

namespace VidCoderCommon
{
	public interface IHandBrakeWorker
	{
		void SetUpWorker(
			int verbosity,
			int previewCount,
			bool useDvdNav,
			double minTitleDurationSeconds,
			double cpuThrottlingFraction,
			string tempFolder);

		void TearDownWorker();

		string Ping();
	}
}
