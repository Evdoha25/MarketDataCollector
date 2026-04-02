using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var rawChannel = Channel.CreateBounded<RawTick>(
    new BoundedChannelOptions(capacity: 1000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = false,
        SingleReader = true
    });

var normalizedChannel = Channel.CreateBounded<NormalizedTick>(
    new BoundedChannelOptions(capacity: 1000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = true
});

builder.Services.AddSingleton(rawChannel);
builder.Services.AddSingleton(normalizedChannel);

var sourceConfig = builder.Configuration.GetSection("Sources");

var appleSettings = sourceConfig.GetSection("AppleSource").Get<SourceSettings>()!;
var samsungSettings = sourceConfig.GetSection("SamsungSource").Get<SourceSettings>()!;

builder.Services.AddSingleton<IMarketDataSource>(sp =>
    new AppleStockSource(appleSettings, sp.GetRequiredService<ILogger<AppleStockSource>>()));

builder.Services.AddSingleton<IMarketDataSource>(sp => 
    new SamsungStockSource(samsungSettings, sp.GetRequiredService<ILogger<SamsungStockSource>>()));

builder.Services.AddSingleton<ITickNormalizer, AppleNormalizer>();
builder.Services.AddSingleton<ITickNormalizer, SamsungNormalizer>();

builder.Services.AddSingleton<IDeduplicator, SlidingWindowDeduplicator>();
builder.Services.AddSingleton<ITickCounter, InMemoryTickCounter>();
builder.Services.AddSingleton<TickProgressService>();

builder.Services.AddSingleton<ITickRepository, SqliteTickRepository>();

builder.Services.AddHostedService<SourceReaderWorker>();
builder.Services.AddHostedService<ProcessingWorker>();
builder.Services.AddHostedService<BatchWriterWorker>();
builder.Services.AddHostedService<MonitoringWorker>();

var app = builder.Build();

Console.WriteLine("Press ctrl + c to stop");

await app.RunAsync();