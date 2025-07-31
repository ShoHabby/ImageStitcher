using DotMake.CommandLine;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

public enum Direction
{
    Horizontal,
    Vertical,

    // For parsing purposes
    [UsedImplicitly, Obsolete($"Use {nameof(Horizontal)} instead", true)]
    H = Horizontal,
    [UsedImplicitly, Obsolete($"Use {nameof(Vertical)} instead", true)]
    V = Vertical
}

[CliCommand(Description = "Image stitcher utility")]
public class ImageStitcherCommand(ILogger<ImageStitcherCommand> logger) : ICliRunAsyncWithContextAndReturn
{
    private ILogger Logger { get; } = logger;

    [CliArgument(Description = "Direction to stitch the files in, h for horizontal, v for vertical",
                 Arity = CliArgumentArity.ExactlyOne, AllowedValues = ["h", "v"])]
    public Direction Direction { get; set; }

    [CliArgument(Description = "List of files to stitch together",
                 Arity = CliArgumentArity.ZeroOrMore, ValidationRules = CliValidationRules.ExistingFileOrDirectory)]
    public FileSystemInfo[] Data { get; set; } = [];

    [CliOption(Description = "If all subfolders at the callsite should be stitched together, <data> should *not* be used with this option",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-a")]
    public bool AllSubfolders { get; set; }

    [CliOption(Description = "If the files should be stitched in reverse direction, horizontal default is right to left, vertical is top to bottom",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-r")]
    public bool Reverse { get; set; }

    [CliOption(Description = "Output file prefix", Arity = CliArgumentArity.ZeroOrOne, Alias = "-p")]
    public string Prefix { get; set; } = string.Empty;

    [CliOption(Description = "Output file separator", Arity = CliArgumentArity.ZeroOrOne, Alias = "-s")]
    public string Separator { get; set; } = "-";

    /// <inheritdoc />
    public Task<int> RunAsync(CliContext cliContext)
    {
        cliContext.ShowValues();
        return Task.FromResult(0);
    }
}
