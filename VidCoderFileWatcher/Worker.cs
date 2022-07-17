using PipeMethodCalls;
using PipeMethodCalls.NetJson;
using VidCoderCommon;
using VidCoderFileWatcher.Model;
using VidCoderFileWatcher.Services;

namespace VidCoderFileWatcher;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
		try
		{
			string pipeName = CommonUtilities.Beta ? "VidCoderBetaWatcher" : "VidCoderWatcher";
			var service = new WatcherService();
			PipeServer<IWatcherCommands> server = new PipeServer<IWatcherCommands>(new NetJsonPipeSerializer(), pipeName, () => service);

			while (!stoppingToken.IsCancellationRequested)
			{
				await server.WaitForConnectionAsync(stoppingToken);
				await server.WaitForRemotePipeCloseAsync(stoppingToken);
			}
		}
		catch (OperationCanceledException)
		{
			// This is expected when the service is stopped
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{Message}", ex.Message);

			// Terminates this process and returns an exit code to the operating system.
			// This is required to avoid the 'BackgroundServiceExceptionBehavior', which
			// performs one of two scenarios:
			// 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
			// 2. When set to "StopHost": will cleanly stop the host, and log errors.
			//
			// In order for the Windows Service Management system to leverage configured
			// recovery options, we need to terminate the process with a non-zero exit code.
			Environment.Exit(1);
		}
	}
}
