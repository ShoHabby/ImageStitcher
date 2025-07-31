using DotMake.CommandLine;
using ImageStitcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Cli.Ext.ConfigureServices(services =>
{
    services.AddLogging(builder =>
    {
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine      = true;
            options.IncludeScopes   = true;
            options.UseUtcTimestamp = false;
            options.TimestampFormat = "HH:mm:ss ";
        });
    });
});

await Cli.RunAsync<ImageStitcherCommand>(args);
