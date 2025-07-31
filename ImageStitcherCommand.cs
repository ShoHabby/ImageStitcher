using System.Collections.Frozen;
using DotMake.CommandLine;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

/// <summary>
/// Stitching direction enum
/// </summary>
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

/// <summary>
/// Subdirectory to stitch data
/// </summary>
/// <param name="Directory">Directory object</param>
/// <param name="Files">Valid files of directory to stitch</param>
public readonly record struct StitchDir(DirectoryInfo Directory, FileInfo[] Files);

/// <summary>
/// Image stitcher command
/// </summary>
/// <param name="logger">Command logger</param>
/// <param name="stitcher">Stitcher service</param>
[CliCommand(Description = "Image stitcher utility")]
public class ImageStitcherCommand(ILogger<ImageStitcherCommand> logger, Stitcher stitcher) : ICliRunAsyncWithContextAndReturn
{
    /// <summary>
    /// Valid extensions list
    /// </summary>
    private static readonly FrozenSet<string> ValidExtensions =
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

    /// <summary>
    /// Command logger
    /// </summary>
    private ILogger Logger { get; } = logger;

    /// <summary>
    /// Stitcher service
    /// </summary>
    private Stitcher Stitcher { get; } = stitcher;

    /// <summary>
    /// Direction to stitch the files in
    /// </summary>
    [CliArgument(Description = "Direction to stitch the files in, h for horizontal, v for vertical",
                 Arity = CliArgumentArity.ExactlyOne, AllowedValues = ["h", "v"])]
    public Direction Direction { get; set; }

    /// <summary>
    /// List of files to stitch together
    /// </summary>
    [CliArgument(Description = "List of files to stitch together",
                 Arity = CliArgumentArity.ZeroOrMore, ValidationRules = CliValidationRules.ExistingFile)]
    public FileInfo[] Files { get; set; } = [];

    /// <summary>
    /// If all subdirectories should be stitched together
    /// </summary>
    [CliOption(Description = "Stitches all the files in the subdirectories of --root-dir instead of a list of files, defaults to current working directory if unspecified",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-a")]
    public bool AllSubdirs { get; set; }

    /// <summary>
    /// Root directory from where to stitch subdirectories when stitching them all together
    /// </summary>
    [CliOption(Description = "Root directory from where to stitch subdirectories with --all-subdirs",
               Required = false, Arity = CliArgumentArity.ZeroOrOne, Alias = "-d", ValidationRules = CliValidationRules.ExistingDirectory)]
    public DirectoryInfo? RootDir { get; set; }

    /// <summary>
    /// Search filter for files in subdirectories when stitching them all together
    /// </summary>
    [CliOption(Description = "Search filter for files in subdirectories when using --all-subdirs, can contain * and ? wildcards",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-f")]
    public string FileFilter { get; set; } = "*";

    /// <summary>
    /// Search filter for subdirectories in the root directory when stitching them all together
    /// </summary>
    [CliOption(Description = "Search filter for subdirectories in the root directory when using --all-subdirs, can contain * and ? wildcards",
               Arity = CliArgumentArity.ZeroOrOne)]
    public string DirectoryFilter { get; set; } = "*";

    /// <summary>
    /// If the files should be stitched in reverse direction, horizontal default is right to left, vertical is top to bottom
    /// </summary>
    [CliOption(Description = "If the files should be stitched in reverse direction, horizontal default is right to left, vertical is top to bottom",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-r")]
    public bool Reverse { get; set; }

    /// <summary>
    /// Output file prefix
    /// </summary>
    [CliOption(Description = "Output file prefix", Arity = CliArgumentArity.ZeroOrOne, Alias = "-p")]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Output file separator
    /// </summary>
    [CliOption(Description = "Output file separator", Arity = CliArgumentArity.ZeroOrOne, Alias = "-s")]
    public string Separator { get; set; } = "-";

    /// <inheritdoc />
    public async Task<int> RunAsync(CliContext context)
    {
#if DEBUG
        context.ShowValues();
#endif

        // Subdirs stitching
        if (this.AllSubdirs)
        {
            if (this.Files is not [])
            {
                this.Logger.LogError("Cannot use --all-subdirs option when <files> are specified.");
                return 1;
            }

            return await RunStitchAllSubfolders(context);
        }

        // Files stitching
        switch (this.Files.Length)
        {
            case 0:
                this.Logger.LogError("Either <files> or --all-subdirs need to be specified");
                return 1;

            case > 0 when this.RootDir is not null:
                this.Logger.LogError("Cannot specify --root-dir when passing files");
                return 1;

            case 1:
                this.Logger.LogWarning("Only one file specified, no stitching to do");
                return 0;

            default:
                return await RunStitchFiles(context);
        }
    }

    /// <summary>
    /// Setups the command for stitching all subdirectories together
    /// </summary>
    /// <param name="context">CLI Context instance</param>
    /// <returns>Exit code</returns>
    private async Task<int> RunStitchAllSubfolders(CliContext context)
    {
        List<StitchDir> stitchDirs = [];
        this.RootDir ??= new DirectoryInfo(Environment.CurrentDirectory);
        foreach (DirectoryInfo directory in this.RootDir.EnumerateDirectories(this.DirectoryFilter))
        {
            // Get list of valid files
            FileInfo[] validFiles = directory.EnumerateFiles(this.FileFilter)
                                             .Where(f => ValidExtensions.Contains(f.Extension))
                                             .ToArray();
            switch (validFiles.Length)
            {
                // If none found, ignore
                case 0:
                    continue;

                // Not enough files, warn
                case 1:
                    this.Logger.LogWarning("Subdirectory {Subdir} only has one stitchable file, skipping", directory.FullName);
                    continue;

                // Mismatched file extensions, warn
                case > 1 when !AllExtensionsEqual(validFiles):
                    this.Logger.LogWarning("Subdirectory {Subdir} found files with mismatched extensions, skipping", directory.FullName);
                    continue;

                // Valid files found, add to directories to stitch
                default:
                    stitchDirs.Add(new StitchDir(directory, validFiles));
                    break;

            }
        }

        // None found, error out
        if (stitchDirs is [])
        {
            this.Logger.LogError("No subdirectories contain valid files to stitch");
            return 1;
        }

        // Send request to stitch all subfolders
        await this.Stitcher.StitchSubfolders(this, stitchDirs, context.CancellationToken);
        return 0;
    }

    /// <summary>
    /// Setups the command for stitching the selected files together
    /// </summary>
    /// <param name="context">CLI Context instance</param>
    /// <returns>Exit code</returns>
    private async Task<int> RunStitchFiles(CliContext context)
    {
        // Invalid extension, error out
        string extension = this.Files[0].Extension;
        if (!ValidExtensions.Contains(extension))
        {
            this.Logger.LogError("Unknown file extension to stitch \"{Extension}\"", extension);
            return 1;
        }

        // Mismatched extensions, error out
        if (!AllExtensionsEqual(this.Files))
        {
            this.Logger.LogError("All file extensions to stitch must be the same");
            return 1;
        }

        // Send request to stitch selected files
        await this.Stitcher.StitchFiles(this, this.Files, GenerateOutputName(this.Files), context.CancellationToken);
        return 0;
    }

    /// <summary>
    /// Generates the output file name for a collection of files to stitch
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <returns>The resulting stitched file name</returns>
    private string GenerateOutputName(FileInfo[] files)
    {
        string result = string.Join(this.Separator, files.Select(f => f.Name)) + files[0].Extension;
        if (!string.IsNullOrEmpty(this.Prefix))
        {
            result = this.Prefix + result;
        }
        return result;
    }

    /// <summary>
    /// Generates the output file name for a subdirectory to stitch
    /// </summary>
    /// <param name="subdirectory">Subdirectory to stitch</param>
    /// <returns>The resulting stitching file name</returns>
    public string GenerateOutputName(StitchDir subdirectory)
    {
        string result = subdirectory.Directory.Name + subdirectory.Files[0].Extension;
        if (!string.IsNullOrEmpty(this.Prefix))
        {
            result = this.Prefix + result;
        }
        return result;
    }

    /// <summary>
    /// Checks whether all the files specified have the same file extension
    /// </summary>
    /// <param name="files">Files to check</param>
    /// <returns><see langword="true"/> if the files have the same extension, otherwise <see langword="false"/></returns>
    private static bool AllExtensionsEqual(ReadOnlySpan<FileInfo> files)
    {
        string first = files[0].Extension;
        foreach (FileInfo other in files[1..])
        {
            if (first != other.Extension)
            {
                return false;
            }
        }

        return true;
    }
}
