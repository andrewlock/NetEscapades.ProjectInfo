Changelog
--- 

## [v0.1.0-beta1](https://github.com/andrewlock/NetEscapades.ProjectInfo/commits/v0.1.0-beta1)

### Features

* Add `<ProjectInfoString>` and `<ProjectInfoUtf8>` items, which are echoed into a generated `ProjectInfo` class as `const string` values and `ReadOnlySpan<byte>` UTF-8 literals
* Add the `ProjectInfoVisibility` MSBuild property to make the generated class `public`
* Add diagnostics NEPI001-NEPI005 for invalid items
