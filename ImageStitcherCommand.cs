using DotMake.CommandLine;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

[CliCommand(Description = "Image stitcher utility")]
public class ImageStitcherCommand(ILogger<ImageStitcherCommand> logger) : ICliRunAsyncWithContextAndReturn
{
    private ILogger Logger { get; } = logger;

    /// <inheritdoc />
    public Task<int> RunAsync(CliContext cliContext)
    {
        this.Logger.LogInformation("Hello, world.");
        cliContext.ShowHelp();
        return Task.FromResult(0);
    }
}
