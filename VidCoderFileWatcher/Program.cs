using VidCoderCommon;
using VidCoderFileWatcher;

IHost host = Host.CreateDefaultBuilder(args)
	.UseWindowsService(options =>
	{
		options.ServiceName = CommonUtilities.WatcherServiceName;
	})
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
