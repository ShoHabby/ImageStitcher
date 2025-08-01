using System.ComponentModel;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace ImageStitcher;

/// <summary>
/// Image stitcher service
/// </summary>
/// <param name="logger">Stitcher logger</param>
public class Stitcher(ILogger<Stitcher> logger)
{
    /// <summary>
    /// Stitcher logger
    /// </summary>
    private ILogger Logger { get; } = logger;

    /// <summary>
    /// Stitches files in the given subfolders
    /// </summary>
    /// <param name="command">Command data</param>
    /// <param name="subfolders">Subfolders to stitch</param>
    /// <param name="token">Cancellation token</param>
    public async Task StitchSubfolders(ImageStitcherCommand command, IReadOnlyList<StitchDir> subfolders, CancellationToken token)
    {
        this.Logger.LogInformation("Stitching {Count} subfolder(s)...", subfolders.Count);
        await Parallel.ForEachAsync(subfolders, token, async (subfolder, cancellationToken) =>
        {
            this.Logger.LogInformation("Stitching subfolder {Subfolder}", subfolder.Directory.FullName);
            await StitchFiles(command, subfolder.Files, command.GenerateOutputName(subfolder), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Stitches the given files together
    /// </summary>
    /// <param name="command">Command data</param>
    /// <param name="files">Files to stitch</param>
    /// <param name="outputName">Stitched file name</param>
    /// <param name="token">Cancellation token</param>
    public async ValueTask StitchFiles(ImageStitcherCommand command, FileInfo[] files, string outputName, CancellationToken token)
    {
        using MagickImageCollection original = new();
        if (command.Reverse || command.Direction is Direction.Horizontal)
        {
            for (int i = files.Length - 1; i >= 0; i--)
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

        using IMagickImage<byte> stitched = command.Direction switch
        {
            Direction.Horizontal => original.AppendHorizontally(),
            Direction.Vertical   => original.AppendVertically(),
            _                    => throw new InvalidEnumArgumentException(nameof(command.Direction), (int)command.Direction, typeof(Direction))
        };

        DirectoryInfo outputDir = command.RootDir ?? files[0].Directory!;
        if (!outputDir.Exists)
        {
            outputDir.Create();
        }

        string outputPath = Path.Combine(outputDir.FullName, outputName);
        await stitched.WriteAsync(outputPath, token).ConfigureAwait(false);
        this.Logger.LogInformation("Stitched file {Path}", outputPath);
    }
}
