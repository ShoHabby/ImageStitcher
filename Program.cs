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

int result;
if (args is [])
{
    result = await Cli.RunAsync<ImageStitcherCommand>("-h");
}
else
{
    try
    {
        result = await Cli.RunAsync<ImageStitcherCommand>(args);
    }
    catch (Exception e)
    {
        Console.WriteLine(e);
        result = 1;
    }
}

return result;
