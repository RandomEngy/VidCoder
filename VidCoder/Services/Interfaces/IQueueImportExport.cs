namespace VidCoder.Services
{
	using System.Collections.Generic;
	using Model;

	public interface IQueueImportExport
	{
		void Import(string queueFile);
		void Export(IList<EncodeJobWithMetadata> jobPersistGroup);
	}
}