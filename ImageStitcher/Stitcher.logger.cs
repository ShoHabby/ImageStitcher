using Microsoft.Extensions.Logging;

namespace ImageStitcher;

public partial class Stitcher
{
    [LoggerMessage(LogLevel.Information, "Stitching {Count} subfolder(s)...")]
    static partial void LogStitchSubfoldersCount(ILogger logger, int count);

    [LoggerMessage(LogLevel.Information, "Stitching subfolder {Subfolder}")]
    static partial void LogStitchSubfolder(ILogger logger, string subfolder);

    [LoggerMessage(LogLevel.Information, "Stitched file {Path}")]
    static partial void LogStitchFile(ILogger logger, string path);
}
