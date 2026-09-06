using System.Collections.Frozen;
using System.ComponentModel;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

/// <summary>
/// Image stitcher service
/// </summary>
/// <param name="logger">Stitcher logger</param>
public class Stitcher(ILogger<Stitcher> logger, StitchOptions options)
{
    /// <summary>
    /// Valid extensions list
    /// </summary>
    public static FrozenSet<string> ValidExtensions { get; } =
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
    /// Stitch options
    /// </summary>
    private StitchOptions Options { get; } = options;

    /// <summary>
    /// Stitcher logger
    /// </summary>
    private ILogger Logger { get; } = logger;

    /// <summary>
    /// Stitches files in the given subfolders
    /// </summary>
    /// <param name="subfolders">Subfolders to stitch</param>
    /// <param name="token">Cancellation token</param>
    public async Task StitchSubfolders(IReadOnlyList<StitchDirectory> subfolders, CancellationToken token = default)
    {
        this.Logger.LogInformation("Stitching {Count} subfolder(s)...", subfolders.Count);
        await Parallel.ForEachAsync(subfolders, token, async (subfolder, cancellationToken) =>
        {
            this.Logger.LogInformation("Stitching subfolder {Subfolder}", subfolder.Directory.FullName);
            await StitchFiles(subfolder.Files, GenerateOutputName(subfolder), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Stitches the given files together
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <param name="token">Cancellation token</param>
    public ValueTask StitchFiles(IReadOnlyList<FileInfo> files, CancellationToken token = default) => StitchFiles(files, GenerateOutputName(files), token);

    /// <summary>
    /// Stitches the given files together
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <param name="outputName">Stitched file name</param>
    /// <param name="token">Cancellation token</param>
    private async ValueTask StitchFiles(IReadOnlyList<FileInfo> files, string outputName, CancellationToken token)
    {
        using MagickImageCollection original = new();
        if (this.Options is { Reverse: true, Direction: StitchDirection.Vertical } or { Reverse: false, Direction: StitchDirection.Horizontal })
        {
            for (int i = files.Count - 1; i >= 0; i--)
            {
                original.Add(new MagickImage(files[i]));
            }
        }
        else
        {
            foreach (FileInfo file in files)
            {
                original.Add(new MagickImage(file));
            }
        }

        using IMagickImage<byte> stitched = this.Options.Direction switch
        {
            StitchDirection.Horizontal => original.AppendHorizontally(),
            StitchDirection.Vertical   => original.AppendVertically(),
            _                    => throw new InvalidEnumArgumentException(nameof(options.Direction), (int)this.Options.Direction, typeof(StitchDirection))
        };

        DirectoryInfo outputDir = this.Options.RootDirectory ?? files[0].Directory!;
        if (!outputDir.Exists)
        {
            outputDir.Create();
        }

        string outputPath = Path.Combine(outputDir.FullName, outputName);
        await stitched.WriteAsync(outputPath, token).ConfigureAwait(false);
        this.Logger.LogInformation("Stitched file {Path}", outputPath);
    }

    /// <summary>
    /// Generates the output file name for a collection of files to stitch
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <returns>The resulting stitched file name</returns>
    private string GenerateOutputName(IReadOnlyList<FileInfo> files)
    {
        string result = string.Join(this.Options.Separator, files.Select(f => Path.ChangeExtension(f.Name, null))) + files[0].Extension;
        if (!string.IsNullOrEmpty(this.Options.Prefix))
        {
            result = this.Options.Prefix + result;
        }
        return result;
    }

    /// <summary>
    /// Generates the output file name for a subdirectory to stitch
    /// </summary>
    /// <param name="subdirectory">Subdirectory to stitch</param>
    /// <returns>The resulting stitching file name</returns>
    private string GenerateOutputName(in StitchDirectory subdirectory)
    {
        string result = subdirectory.Directory.Name + subdirectory.Files[0].Extension;
        if (!string.IsNullOrEmpty(this.Options.Prefix))
        {
            result = this.Options.Prefix + this.Options.Separator + result;
        }
        return result;
    }
}
