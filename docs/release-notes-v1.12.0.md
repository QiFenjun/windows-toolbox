# Windows Toolbox v1.12.0

This release adds two offline tools inside Utilities without adding another sidebar module.

- **Image Tools** reads PNG, JPEG and BMP images; previews metadata; resizes with aspect-ratio and no-upscale options; and converts between PNG, JPEG and BMP. Batch work is sequential and cancellable. Outputs use new suffixed paths and do not overwrite the source or an existing file. EXIF orientation is applied, while source metadata is stripped; transparent pixels become white for JPEG/BMP output.
- **Regex Tools** runs .NET regular expressions with a selectable 100–2,000 ms timeout, displays matches and capture groups, and provides a read-only replacement preview. Large inputs require an explicit run; match and output sizes are bounded.

Both tools run locally and do not save or upload image contents, paths, patterns, test text, groups or results. See [v1.12.0 validation](v1.12.0-validation.md) for test results and remaining manual acceptance items.
