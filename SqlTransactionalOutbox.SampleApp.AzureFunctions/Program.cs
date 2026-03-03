using Functions.Worker.HttpResponseDataCompression;
using Functions.Worker.HttpResponseDataJsonMiddleware;
using Microsoft.Extensions.Hosting;

var host = Host
    .CreateDefaultBuilder()
    .ConfigureFunctionsWorkerDefaults(appBuilder =>
    {
        appBuilder
            .UseHttpResponseDataCompression()
            .UseJsonResponses();
    })
    .Build();

await host.RunAsync().ConfigureAwait(false);