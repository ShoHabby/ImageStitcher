using DotMake.CommandLine;
using ImageStitcher;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

// DI Configuration
Cli.Ext.ConfigureServices(services =>
{
    Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .Enrich.FromLogContext()
                .CreateLogger();

    services.AddLogging(builder =>
    {
        builder.AddSerilog(Log.Logger, true);
    });

    services.AddSingleton<Stitcher>();
});

if (args is [])
{
    args = ["-h"];
}

int result;
try
{
    // Try running the command
    result = await Cli.RunAsync<ImageStitcherCommand>(args).ConfigureAwait(false);
}
catch (Exception e)
{
    // Log exceptions
    Log.Error(e, "An error occured while executing the command.");
    result = 1;
}

return result;
