using Functions.Worker.HttpResponseDataCompression;
using Functions.Worker.HttpResponseDataJsonMiddleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlTransactionalOutbox;
using SqlTransactionalOutbox.SampleApp.AzureFunctions;
using System;

var host = Host
    .CreateDefaultBuilder()
    .ConfigureFunctionsWorkerDefaults(appBuilder =>
    {
        appBuilder
            .UseHttpResponseDataCompression()
            .UseJsonResponses();
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<ISampleAppConfig, SampleAppConfig>();
    })
    .Build();

var appConfig = host.Services.GetService<ISampleAppConfig>();

SqlTransactionalOutboxInitializer.Configure(config =>
{
    config
        .WithDistributedMutexLockSettings(
            lockNamePrefix: "SqlServerTransactionalOutboxProcessor::",
            lockAcquisitionTimeoutSeconds: 3
        )
        .ConfigureOutboxProcessingOptions(options =>
        {
            //ItemProcessingBatchSize = 200, //Only process the top X items to keep this function responsive!
            options.FifoEnforcedPublishingEnabled = true; //The Service Bus Topic is Session Enabled so we must processes it with FIFO Processing Enabled!
            options.MaxPublishingAttempts = appConfig.OutboxMaxPublishingRetryAttempts;
            options.TimeSpanToLive = appConfig.OutboxMaxTimeToLiveTimeSpan;
            options.MaxPublishingAttempts = 5;
            options.TimeSpanToLive = TimeSpan.FromHours(1);
            //Optimize our Scheduled Delivery by pre-fetching items . . . 
            //NOTE: By tuning this value in combination with the Outbox Processing Interval (e.g. TransactionalOutboxAgentCronSchedule) you can ensure item delivery is
            //  as close as possible to the actual Scheduled Publish Time while still allowing for some tolerance to ensure items are not missed due to processing delays, etc.!
            //FOR EXAMPLE: If your Outbox Processing Interval is every 1 minute and you want to ensure items are published as close as possible to their Scheduled Publish Time
            //  then you may want to set the prefetch time to 2 minutes (or 5 minutes) -- slightly to marginally higher than the processing interval -- to ensure items
            //  are published to Azure Service Bus before their scheduled time and allow some tolerance for processing delays, etc.!
            //  This may also be impacted by other configuration options such as overall outbox load/throughput, processing batch sizes, etc.
            options.ScheduledPublishPrefetchTime = TimeSpan.FromMinutes(2);
        });
});

await host.RunAsync().ConfigureAwait(false);