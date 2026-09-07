using System.Collections.Frozen;
using System.ComponentModel;
using System.Text;
using ImageMagick;
using ImageStitcher.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

/// <summary>
/// Image stitcher service
/// </summary>
/// <param name="logger">Stitcher logger</param>
/// <param name="options">Stitch options</param>
[PublicAPI]
public sealed partial class Stitcher(StitchOptions options, ILogger<Stitcher> logger)
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

    /// <summary> Output file name StringBuilder </summary>
    private readonly StringBuilder outputBuilder = new(100);

    /// <summary>
    /// Stitch options
    /// </summary>
    private StitchOptions Options { get; } = options;

    /// <summary>
    /// Stitcher logger
    /// </summary>
    private ILogger Logger { get; } = logger;

    /// <summary>
    /// Creates a new Stitcher with default options
    /// </summary>
    /// <param name="logger">Stitcher logger</param>
    public Stitcher(ILogger<Stitcher> logger) : this(StitchOptions.DefaultOptions, logger) { }

    /// <summary>
    /// Stitches files in the given subfolders
    /// </summary>
    /// <param name="subfolders">Subfolders to stitch</param>
    /// <param name="token">Cancellation token</param>
    /// <exception cref="ArgumentException">If <paramref name="subfolders"/> is an empty list</exception>
    /// <exception cref="InvalidEnumArgumentException">If the provided <see cref="StitchDirection"/> in the <see cref="Options"/> is invalid</exception>
    public async Task StitchSubfolders(IReadOnlyList<StitchDirectory> subfolders, CancellationToken token = default)
    {
        if (subfolders is []) throw new ArgumentException("No subfolders to stitch", nameof(subfolders));

        LogStitchSubfoldersCount(this.Logger, subfolders.Count);
        await Parallel.ForEachAsync(subfolders, token, StitchSubfolder).ConfigureAwait(false);
    }

    /// <summary>
    /// Stitches the given files together
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <param name="token">Cancellation token</param>
    /// <exception cref="ArgumentException">If <paramref name="files"/> is an empty list</exception>
    /// <exception cref="InvalidEnumArgumentException">If the provided <see cref="StitchDirection"/> in the <see cref="Options"/> is invalid</exception>
    public ValueTask StitchFiles(IReadOnlyList<FileInfo> files, CancellationToken token = default)
    {
        return files is not []
                   ? StitchFiles(files, GenerateOutputName(files), token)
                   : throw new ArgumentException("No files to stitch", nameof(files));
    }

    /// <summary>
    /// Stitches files in the given subfolder
    /// </summary>
    /// <param name="subfolder">Subfolder to stitch</param>
    /// <param name="token">Cancellation token</param>
    /// <exception cref="InvalidEnumArgumentException">If the provided <see cref="StitchDirection"/> in the <see cref="Options"/> is invalid</exception>
    private ValueTask StitchSubfolder(StitchDirectory subfolder, CancellationToken token)
    {
        LogStitchSubfolder(this.Logger, subfolder.Directory.FullName);
        return StitchFiles(subfolder.Files, GenerateOutputName(subfolder), token);
    }

    /// <summary>
    /// Stitches the given files together
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <param name="outputName">Stitched file name</param>
    /// <param name="token">Cancellation token</param>
    /// <exception cref="InvalidEnumArgumentException">If the provided <see cref="StitchDirection"/> in the <see cref="Options"/> is invalid</exception>
    private async ValueTask StitchFiles(IReadOnlyList<FileInfo> files, string outputName, CancellationToken token)
    {
        if (files is []) throw new ArgumentException("No files to stitch", nameof(files));

        token.ThrowIfCancellationRequested();
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
            _                          => throw new InvalidEnumArgumentException(nameof(options.Direction), (int)this.Options.Direction, typeof(StitchDirection))
        };

        DirectoryInfo outputDir = this.Options.RootDirectory ?? files[0].Directory!;
        if (!outputDir.Exists)
        {
            outputDir.Create();
        }

        string outputPath = Path.Combine(outputDir.FullName, outputName);
        await stitched.WriteAsync(outputPath, token).ConfigureAwait(false);
        LogStitchFile(this.Logger, outputPath);
    }

    /// <summary>
    /// Generates the output file name for a collection of files to stitch
    /// </summary>
    /// <param name="files">Files to stitch</param>
    /// <returns>The resulting stitched file name</returns>
    private string GenerateOutputName(IReadOnlyList<FileInfo> files)
    {
        if (!string.IsNullOrEmpty(this.Options.Prefix))
        {
            this.outputBuilder.Append(this.Options.Prefix)
                              .Append(this.Options.Separator);
        }

        return this.outputBuilder.AppendJoin(this.Options.Separator, files.Select(f => Path.ChangeExtension(f.Name, null)))
                                 .Append(files[0].Extension)
                                 .ToStringAndClear();
    }

    /// <summary>
    /// Generates the output file name for a subdirectory to stitch
    /// </summary>
    /// <param name="subdirectory">Subdirectory to stitch</param>
    /// <returns>The resulting stitching file name</returns>
    private string GenerateOutputName(StitchDirectory subdirectory)
    {
        if (!string.IsNullOrEmpty(this.Options.Prefix))
        {
            this.outputBuilder.Append(this.Options.Prefix)
                              .Append(this.Options.Separator);
        }

        return this.outputBuilder.Append(subdirectory.Directory.Name)
                                 .Append(subdirectory.Files[0].Extension)
                                 .ToStringAndClear();
    }
}
