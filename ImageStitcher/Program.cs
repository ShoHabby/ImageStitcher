using DotMake.CommandLine;
using ImageStitcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// DI Configuration
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

    services.AddSingleton<Stitcher>();
});

int result;
if (args is [])
{
    // If no args, print help
    result = await Cli.RunAsync<ImageStitcherCommand>("-h").ConfigureAwait(false);
}
else
{
    try
    {
        // Try running the command
        result = await Cli.RunAsync<ImageStitcherCommand>(args).ConfigureAwait(false);
    }
    catch (Exception e)
    {
        // Log exceptions
        await Console.Error.WriteLineAsync($"[{e.GetType().Name}]: {e.Message}\n{e.StackTrace}").ConfigureAwait(false);
        result = 1;
    }
}

return result;
