using VidCoderCommon;
using VidCoderFileWatcher;

IHost host = Host.CreateDefaultBuilder(args)
	.UseWindowsService(options =>
	{
		options.ServiceName = CommonUtilities.Beta ? "VidCoder Beta File Watcher" :  "VidCoder File Watcher";
	})
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
