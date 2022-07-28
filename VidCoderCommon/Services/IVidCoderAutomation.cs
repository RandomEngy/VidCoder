﻿namespace VidCoderCommon.Services
{
	public interface IVidCoderAutomation
	{
		void Encode(string cli_timeSpan, string source, string destination, string preset, string picker);

		void Scan(string source);

		void ImportPreset(string filePath);

		void ImportQueue(string filePath);

		void BringToForeground();
	}
}