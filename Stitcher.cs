using Microsoft.Extensions.Logging;

namespace ImageStitcher;

public class Stitcher(ILogger<Stitcher> logger)
{
    private ILogger Logger { get; } = logger;
}
