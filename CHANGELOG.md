Changelog
--- 

## [v0.1.0-beta3](https://github.com/andrewlock/NetEscapades.ProjectInfo/compare/v0.1.0-beta2...v0.1.0-beta3)

### Fixes

* Fix builds failing on MSBuild versions earlier than 18.3 (e.g. the .NET 7, 8 and 9 SDKs), which don't allow `System.Uri` in property functions. 

## [v0.1.0-beta2](https://github.com/andrewlock/NetEscapades.ProjectInfo/compare/v0.1.0-beta1...v0.1.0-beta2)

### Fixes

* Fix NEPI001 (empty item name) when only one of `<ProjectInfoString>` or `<ProjectInfoUtf8>` is declared

## [v0.1.0-beta1](https://github.com/andrewlock/NetEscapades.ProjectInfo/commits/v0.1.0-beta1)

### Features

* Add `<ProjectInfoString>` and `<ProjectInfoUtf8>` items, which are echoed into a generated `ProjectInfo` class as `const string` values and `ReadOnlySpan<byte>` UTF-8 literals
* Add the `ProjectInfoVisibility` MSBuild property to make the generated class `public`
* Add diagnostics NEPI001-NEPI005 for invalid items
