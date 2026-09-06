using JetBrains.Annotations;

namespace ImageStitcher;

/// <summary>
/// Stitching options
/// </summary>
/// <param name="Direction">Direction to stitch the files in</param>
/// <param name="RootDirectory">Root directory from where to stitch subdirectories when stitching them all together</param>
/// <param name="Reverse">If the files should be stitched in reverse direction, horizontal default is right to left, vertical is top to bottom</param>
/// <param name="Prefix">Output file prefix</param>
/// <param name="Separator">Output file separator</param>
[PublicAPI]
public readonly record struct StitchOptions(StitchDirection Direction,
                                             DirectoryInfo? RootDirectory = null,
                                             bool Reverse = false,
                                             string Prefix = StitchOptions.DEFAULT_PREFIX,
                                             string Separator = StitchOptions.DEFAULT_SEPARATOR)
{
    /// <summary>
    /// Default file prefix
    /// </summary>
    public const string DEFAULT_PREFIX = "";

    /// <summary>
    /// Default file separator
    /// </summary>
    public const string DEFAULT_SEPARATOR = "-";

    /// <summary>
    /// Default stitch options
    /// </summary>
    public static StitchOptions DefaultOptions { get; } = new();

    /// <summary>
    /// Creates new StitchOptions with default values
    /// </summary>
    public StitchOptions() : this(StitchDirection.Horizontal) { }
}
