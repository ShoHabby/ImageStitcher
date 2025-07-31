using System.Collections.Immutable;
using DotMake.CommandLine;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

public enum Direction
{
    Horizontal,
    Vertical,

    // For parsing purposes
    [Obsolete($"Use {nameof(Horizontal)} instead", true)]
    H = Horizontal,
    [Obsolete($"Use {nameof(Vertical)} instead", true)]
    V = Vertical
}

[CliCommand(Description = "Image stitcher utility")]
public class ImageStitcherCommand(ILogger<ImageStitcherCommand> logger, Stitcher stitcher) : ICliRunAsyncWithContextAndReturn
{
    private static readonly ImmutableArray<string> ValidExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".jfif",
        ".tiff",
        ".bmp",
        ".webp",
        ".avif"
    ];

    private ILogger Logger { get; } = logger;

    private Stitcher Stitcher { get; } = stitcher;

    [CliArgument(Description = "Direction to stitch the files in, h for horizontal, v for vertical",
                 Arity = CliArgumentArity.ExactlyOne, AllowedValues = ["h", "v"])]
    public Direction Direction { get; set; }

    [CliArgument(Description = "List of files to stitch together",
                 Arity = CliArgumentArity.ZeroOrMore, ValidationRules = CliValidationRules.ExistingFile)]
    public FileInfo[] Files { get; set; } = [];

    [CliOption(Description = "If all subfolders at the callsite should be stitched together, <files> should *not* be used with this option",
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
    public async Task<int> RunAsync(CliContext context)
    {
        #if DEBUG
        context.ShowValues();
        #endif

        if (this.AllSubfolders && this.Files is not [])
        {
            return await RunAllSubfolders(context);
        }

        switch (this.Files.Length)
        {
            case 0:
                this.Logger.LogError("Either <files> or --all-subfolder need to be specified");
                return 1;

            case 1:
                this.Logger.LogWarning("Only one file specified, no stitching to do");
                return 0;

            default:
                return await RunFiles(context);
        }
    }

    private async Task<int> RunAllSubfolders(CliContext context)
    {
        if (this.Files is not [])
        {
            this.Logger.LogError("Cannot use --all-subfolders option when <files> are specified.");
            return 1;
        }

        return 0;
    }

    private async Task<int> RunFiles(CliContext context)
    {
        string[] extensions = Array.ConvertAll(this.Files, f => f.Extension);
        if (!AllExtensionsEqual(extensions))
        {
            this.Logger.LogError("All file extensions to stitch must be the same");
            return 1;
        }

        if (!ValidExtensions.Contains(extensions[0]))
        {
            this.Logger.LogError("Unknown file extension to stitch \"{Extension}\"", extensions[0]);
            return 1;
        }

        return 0;
    }

    private static bool AllExtensionsEqual(ReadOnlySpan<string> extensions)
    {
        string first = extensions[0];
        foreach (string other in extensions[1..])
        {
            if (first != other)
            {
                return false;
            }
        }

        return true;
    }
}
