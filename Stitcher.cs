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
        await Parallel.ForEachAsync(subfolders, token, async (subfolder, cancellationToken) =>
        {
            await StitchFiles(command, subfolder.Files, cancellationToken);
        });
    }

    /// <summary>
    /// Stitches the given files together
    /// </summary>
    /// <param name="command">Command data</param>
    /// <param name="files">Files to stitch</param>
    /// <param name="token">Cancellation token</param>
    public async ValueTask StitchFiles(ImageStitcherCommand command, IEnumerable<FileInfo> files, CancellationToken token)
    {
    }
}
