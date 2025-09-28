// See https://aka.ms/new-console-template for more information
using PipeMethodCalls;
using PipeMethodCalls.NetJson;
using VidCoderCommon;
using VidCoderCommon.Model;
using VidCoderCommon.Services;
using VidCoderFileWatcher.Model;
using VidCoderFileWatcher.Services;

IBasicLogger logger = new SupportLogger("Watcher");

try
{
	var service = new WatcherService(logger);
	await service.RefreshFromWatchedFoldersAsync().ConfigureAwait(false);
	CancellationTokenSource tokenSource = new();

	bool firstLineWritten = false;

	while (!tokenSource.IsCancellationRequested)
	{
		var pipeServer = new PipeServer<IWatcherCommands>(new NetJsonPipeSerializer(), CommonUtilities.FileWatcherPipeName, () => service);
		try
		{
			//pipeServer.SetLogger(message => logger.Log(message));
			Task connectTask = pipeServer.WaitForConnectionAsync(tokenSource.Token);

			if (!firstLineWritten)
			{
				// Write a line to let the client know we are ready for connections
				Console.WriteLine("Pipe is open");
				firstLineWritten = true;
			}

			await connectTask.ConfigureAwait(false);
			await pipeServer.WaitForRemotePipeCloseAsync(tokenSource.Token).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			logger.Log(exception.ToString());
		}
		finally
		{
			pipeServer.Dispose();
		}
	}
}
catch (Exception exception)
{
	logger.Log(exception.ToString());
}