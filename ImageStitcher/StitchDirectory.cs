namespace ImageStitcher;

/// <summary>
/// Subdirectory to stitch data
/// </summary>
/// <param name="Directory">Directory object</param>
/// <param name="Files">Valid files of directory to stitch</param>
public readonly record struct StitchDirectory(DirectoryInfo Directory, FileInfo[] Files);
