using System.Buffers;
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
    /// Invalid file name characters
    /// </summary>
    private static readonly SearchValues<char> InvalidFileChars = SearchValues.Create(Path.GetInvalidFileNameChars());

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
    [CliOption(Description = "Root directory from where to stitch subdirectories with --all-subdirs, or where to save the stitched image otherwise",
               Required = false, Arity = CliArgumentArity.ZeroOrOne, Alias = "-d", ValidationRules = CliValidationRules.LegalPath)]
    public DirectoryInfo? RootDir { get; set; }

    /// <summary>
    /// Search filter for files in subdirectories when stitching them all together
    /// </summary>
    [CliOption(Description = "Search filter for files in subdirectories when using --all-subdirs, can contain * and ? wildcards",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-ff")]
    public string FileFilter { get; set; } = "*";

    /// <summary>
    /// Search filter for subdirectories in the root directory when stitching them all together
    /// </summary>
    [CliOption(Description = "Search filter for subdirectories in the root directory when using --all-subdirs, can contain * and ? wildcards",
               Arity = CliArgumentArity.ZeroOrOne, Alias = "-df")]
    public string DirFilter { get; set; } = "*";

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

        // Validate file chars
        if (HasInvalidChars(this.Separator))
        {
            this.Logger.LogError("File name separator ({Separator}) contains invalid character(s) [{Invalid}]", this.Separator, GetInvalidCharsPrettyPrint());
            return 1;
        }
        if (HasInvalidChars(this.Prefix))
        {
            this.Logger.LogError("File name prefix ({Prefix}) contains invalid character(s) [{Invalid}]", this.Prefix, GetInvalidCharsPrettyPrint());
            return 1;
        }

        // Subdirs stitching
        if (this.AllSubdirs)
        {
            if (this.Files is not [])
            {
                this.Logger.LogError("Cannot use --all-subdirs option when <files> are specified.");
                return 1;
            }

            return await RunStitchAllSubfolders(context).ConfigureAwait(false);
        }

        // Files stitching
        switch (this.Files.Length)
        {
            case 0:
                this.Logger.LogError("Either <files> or --all-subdirs need to be specified");
                return 1;

            case 1:
                this.Logger.LogWarning("Only one file specified, no stitching to do");
                return 0;

            default:
                return await RunStitchFiles(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Setups the command for stitching all subdirectories together
    /// </summary>
    /// <param name="context">CLI Context instance</param>
    /// <returns>Exit code</returns>
    private async Task<int> RunStitchAllSubfolders(CliContext context)
    {
        this.RootDir ??= new DirectoryInfo(Environment.CurrentDirectory);
        if (!this.RootDir.Exists)
        {
            this.Logger.LogError("Cannot stitch subfolders of {Directory} as it does not exist", this.RootDir.FullName);
            return 1;
        }

        List<StitchDir> stitchDirs = [];
        foreach (DirectoryInfo directory in this.RootDir.EnumerateDirectories(this.DirFilter))
        {
            // Get list of valid files
            FileInfo[] validFiles = directory.EnumerateFiles(this.FileFilter)
                                             .Where(f => ValidExtensions.Contains(f.Extension))
                                             .ToArray();
            switch (validFiles.Length)
            {
                // If none found, warn
                case 0:
                    this.Logger.LogWarning("Subdirectory {Subdir} has no stitchable file, skipping", directory.FullName);
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
        await this.Stitcher.StitchSubfolders(this, stitchDirs, context.CancellationToken).ConfigureAwait(false);
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
        await this.Stitcher.StitchFiles(this, this.Files, GenerateOutputName(this.Files), context.CancellationToken).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// Generates the output file name for a collection of files to stitch
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <returns>The resulting stitched file name</returns>
    private string GenerateOutputName(FileInfo[] files)
    {
        string result = string.Join(this.Separator, files.Select(f => Path.ChangeExtension(f.Name, null))) + files[0].Extension;
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
            result = this.Prefix + this.Separator + result;
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

    /// <summary>
    /// Checks if a given string value contains invalid file name characters
    /// </summary>
    /// <param name="value">String value to validate</param>
    /// <returns><see langword="true"/> if the value has invalid file characters, otherwise <see langword="false"/></returns>
    private static bool HasInvalidChars(string? value) => !string.IsNullOrEmpty(value)
                                                       && value.AsSpan().ContainsAny(InvalidFileChars);

    /// <summary>
    /// Gets a pretty printable list of invalid file name characters
    /// </summary>
    /// <returns>Enumerable of formatted invalid characters</returns>
    /// ReSharper disable once CognitiveComplexity
    private static IEnumerable<string> GetInvalidCharsPrettyPrint()
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            switch (c)
            {
                case '\0':
                    yield return @"\0";
                    break;
                case '\a':
                    yield return @"\a";
                    break;
                case '\b':
                    yield return @"\b";
                    break;
                case '\f':
                    yield return @"\f";
                    break;
                case '\n':
                    yield return @"\n";
                    break;
                case '\r':
                    yield return @"\r";
                    break;
                case '\t':
                    yield return @"\t";
                    break;
                case '\v':
                    yield return @"\v";
                    break;

                case var _ when char.IsControl(c):
                    continue;

                default:
                    yield return c.ToString();
                    break;
            }
        }
    }
}
