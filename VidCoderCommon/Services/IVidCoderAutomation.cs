namespace VidCoderCommon.Services
{
	public interface IVidCoderAutomation
	{
		void Encode(string _RangeTypeParam, string source, string destination, string preset, string picker);

		void Scan(string source);

		void ImportPreset(string filePath);

		void ImportQueue(string filePath);

		void BringToForeground();
	}
}