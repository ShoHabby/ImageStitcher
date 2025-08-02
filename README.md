# ImageStitcher

ImageStitcher is an ImageMagick powered CLI image stitcher for manga or longstrip usage.

## Installation
Installation instructions can be obtained from [nuget.org](https://www.nuget.org/packages/ShoHabby.ImageStitcher/1.0.0).
ImageStitcher is built with .NET 9.0 and is required to run it.

## Usage
```
Usage:
  stitcher <direction> [<files>...] [options]

Arguments:
  <h|v>    Direction to stitch the files in, h for horizontal, v for vertical [required]
  <files>  List of files to stitch together

Options:
  -a, --all-subdirs                 Stitches all the files in the subdirectories of --root-dir instead of a list of files, defaults to current working directory if unspecified
  -d, --root-dir <root-dir>         Root directory from where to stitch subdirectories with --all-subdirs, or where to save the stitched image otherwise
  -ff, --file-filter <file-filter>  Search filter for files in subdirectories when using --all-subdirs, can contain * and ? wildcards [default: *]
  -df, --dir-filter <dir-filter>    Search filter for subdirectories in the root directory when using --all-subdirs, can contain * and ? wildcards [default: *]
  -r, --reverse                     If the files should be stitched in reverse direction, horizontal default is right to left, vertical is top to bottom
  -p, --prefix <prefix>             Output file prefix
  -s, --separator <separator>       Output file separator [default: -]
  -?, -h, --help                    Show help and usage information
  -v, --version                     Show version information

```
ImageStitcher can stitch together a group of specified files, or analyze a directory containing
subdirectories of files to stitch together.

In file list, a list of images is passed directly to the command, and these images are
stitched together then saved. All images must have the same extension.
In this mode, passing `--root-dir` will instead dictate where the output file is saved.
If it is not passed, the output file is saved in the same directory as the first stitched file.

In subfolder search, the command looks at all the subfolders in the working directory and
stitches all the images found within it. Images must have the same extensions once again.
Subfolder search allows filtering of the selected subfolders, as well as the selected files in the subfolders.

## Supported types
- PNG (.png)
- JPG (.jpg, .jpeg, .jfif)
- TIFF (.tiff)
- BMP (.bmp)
- WebP (.webp)
- AVIF (.avif)
