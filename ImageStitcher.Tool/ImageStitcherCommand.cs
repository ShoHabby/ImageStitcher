using System.Buffers;
using DotMake.CommandLine;
using Microsoft.Extensions.Logging;

namespace ImageStitcher.Tool;

/// <summary>
/// Image stitcher command
/// </summary>
/// <param name="logger">Command logger</param>
/// <param name="stitcherLogger">Stitcher logger</param>
[CliCommand(Description = "Image stitching CLI utility intended for manga/manwha use")]
public sealed partial class ImageStitcherCommand(ILogger<ImageStitcherCommand> logger, ILogger<Stitcher> stitcherLogger) : ICliRunAsyncWithContextAndReturn
{
    /// <summary>
    /// Invalid file name characters
    /// </summary>
    private static readonly SearchValues<char> InvalidFileChars = SearchValues.Create(Path.GetInvalidFileNameChars());

    /// <summary>
    /// Command logger
    /// </summary>
    private ILogger Logger { get; } = logger;

    /// <summary>
    /// Stitcher logger
    /// </summary>
    private ILogger<Stitcher> SticherLogger { get; } = stitcherLogger;

    /// <summary>
    /// Direction to stitch the files in
    /// </summary>
    [CliArgument(Description = "Direction to stitch the files in, h for horizontal, v for vertical",
                 Arity = CliArgumentArity.ExactlyOne, AllowedValues = ["h", "v"])]
    public StitchDirection Direction { get; set; }

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
    public string Prefix { get; set; } = StitchOptions.DEFAULT_PREFIX;

    /// <summary>
    /// Output file separator
    /// </summary>
    [CliOption(Description = "Output file separator", Arity = CliArgumentArity.ZeroOrOne, Alias = "-s")]
    public string Separator { get; set; } = StitchOptions.DEFAULT_SEPARATOR;

    /// <inheritdoc />
    public async Task<int> RunAsync(CliContext context)
    {
        #if DEBUG
        context.ShowValues();
        #endif

        // Validate file chars
        if (HasInvalidChars(this.Separator))
        {
            LogInvalidCharactersInSeparator(this.Logger, this.Separator, GetInvalidCharsPrettyPrint());
            return 1;
        }
        if (HasInvalidChars(this.Prefix))
        {
            LogInvalidCharactersInPrefix(this.Logger, this.Prefix, GetInvalidCharsPrettyPrint());
            return 1;
        }

        // Subdirs stitching
        if (this.AllSubdirs)
        {
            if (this.Files is not [])
            {
                LogCannotUseSubdirsWithFiles(this.Logger);
                return 1;
            }

            return await RunStitchAllSubfolders(context).ConfigureAwait(false);
        }

        // Files stitching
        switch (this.Files.Length)
        {
            case 0:
                LogFilesOrSubdirsNeeded(this.Logger);
                return 1;

            case 1:
                LogOnlyOneFile(this.Logger);
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
            LogDirectoryDoesNotExist(this.Logger, this.RootDir.FullName);
            return 1;
        }

        List<StitchDirectory> stitchDirs = [];
        foreach (DirectoryInfo directory in this.RootDir.EnumerateDirectories(this.DirFilter))
        {
            // Get list of valid files
            FileInfo[] validFiles = [..directory.EnumerateFiles(this.FileFilter)
                                                .Where(f => Stitcher.ValidExtensions.Contains(f.Extension))];
            switch (validFiles.Length)
            {
                // If none found, warn
                case 0:
                    LogNoFilesInSubdirectory(this.Logger, directory.FullName);
                    continue;

                // Not enough files, warn
                case 1:
                    LogOnlyOneFileInSubdirectory(this.Logger, directory.FullName);
                    continue;

                // Mismatched file extensions, warn
                case > 1 when !AllExtensionsEqual(validFiles):
                    LogExtensionMismatchInSubdirectory(this.Logger, directory.FullName);
                    continue;

                // Valid files found, add to directories to stitch
                default:
                    stitchDirs.Add(new StitchDirectory(directory, validFiles));
                    break;
            }
        }

        // None found, error out
        if (stitchDirs is [])
        {
            LogSubdirectoriesWithValidFiles(this.Logger);
            return 1;
        }

        // Send request to stitch all subfolders
        StitchOptions options = new(this.Direction, this.RootDir, this.Reverse, this.Prefix, this.Separator);
        Stitcher stitcher = new(options, this.SticherLogger);
        await stitcher.StitchSubfolders(stitchDirs, context.CancellationToken).ConfigureAwait(false);
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
        if (!Stitcher.ValidExtensions.Contains(extension))
        {
            LogUnknownExtension(this.Logger, extension);
            return 1;
        }

        // Mismatched extensions, error out
        if (!AllExtensionsEqual(this.Files))
        {
            LogExtensionMismatch(this.Logger);
            return 1;
        }

        // Send request to stitch selected files
        StitchOptions options = new(this.Direction, this.RootDir, this.Reverse, this.Prefix, this.Separator);
        Stitcher stitcher = new(options, this.SticherLogger);
        await stitcher.StitchFiles(this.Files, context.CancellationToken).ConfigureAwait(false);
        return 0;
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
