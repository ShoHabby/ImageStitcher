using Microsoft.Extensions.Logging;

namespace ImageStitcher.Tool;

public partial class ImageStitcherCommand
{
    [LoggerMessage(LogLevel.Error, "Unknown file extension to stitch \"{Extension}\"")]
    static partial void LogUnknownExtension(ILogger logger, string extension);

    [LoggerMessage(LogLevel.Error, "All file extensions to stitch must be the same")]
    static partial void LogExtensionMismatch(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Cannot stitch subfolders of {Directory} as it does not exist")]
    static partial void LogDirectoryDoesNotExist(ILogger logger, string directory);

    [LoggerMessage(LogLevel.Warning, "Subdirectory {Subdir} has no stitchable file, skipping")]
    static partial void LogNoFilesInSubdirectory(ILogger logger, string subdir);

    [LoggerMessage(LogLevel.Warning, "Subdirectory {Subdir} only has one stitchable file, skipping")]
    static partial void LogOnlyOneFileInSubdirectory(ILogger logger, string subdir);

    [LoggerMessage(LogLevel.Warning, "Subdirectory {Subdir} found files with mismatched extensions, skipping")]
    static partial void LogExtensionMismatchInSubdirectory(ILogger logger, string subdir);

    [LoggerMessage(LogLevel.Error, "No subdirectories contain valid files to stitch")]
    static partial void LogSubdirectoriesWithValidFiles(ILogger logger);

    [LoggerMessage(LogLevel.Error, "File name separator ({Separator}) contains invalid character(s) [{Invalid}]")]
    static partial void LogInvalidCharactersInSeparator(ILogger logger, string separator, IEnumerable<string> invalid);

    [LoggerMessage(LogLevel.Error, "File name prefix ({Prefix}) contains invalid character(s) [{Invalid}]")]
    static partial void LogInvalidCharactersInPrefix(ILogger logger, string prefix, IEnumerable<string> invalid);

    [LoggerMessage(LogLevel.Error, "Cannot use --all-subdirs option when <files> are specified.")]
    static partial void LogCannotUseSubdirsWithFiles(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Either <files> or --all-subdirs need to be specified")]
    static partial void LogFilesOrSubdirsNeeded(ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Only one file specified, no stitching to do")]
    static partial void LogOnlyOneFile(ILogger logger);
}
