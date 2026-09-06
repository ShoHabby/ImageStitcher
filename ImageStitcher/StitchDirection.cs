namespace ImageStitcher;

/// <summary>
/// Stitching direction enum
/// </summary>
public enum StitchDirection
{
    /// <summary> Horizontal stitching </summary>
    Horizontal,
    /// <summary> Vertical stitching </summary>
    Vertical,

    // For parsing purposes
    /// <summary> Horizontal stitching </summary>
    [Obsolete($"Use {nameof(Horizontal)} instead", true)]
    H = Horizontal,
    /// <summary> Vertical stitching </summary>
    [Obsolete($"Use {nameof(Vertical)} instead", true)]
    V = Vertical
}
