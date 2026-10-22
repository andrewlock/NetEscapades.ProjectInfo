namespace NetEscapades.ProjectInfo;

internal enum ProjectInfoKind : byte
{
    String,
    Utf8,
}

/// <summary>
/// A single &lt;ProjectInfoString&gt; or &lt;ProjectInfoUtf8&gt; item from the project file.
/// <paramref name="Value"/> is null if the item could not be read (MSBuild failed to evaluate it).
/// A struct, so that an <see cref="EquatableArray{T}"/> of entries is a single allocation.
/// </summary>
internal readonly record struct ProjectInfoEntry(ProjectInfoKind Kind, string Name, string? Value);

/// <summary>The MSBuild properties that control code generation</summary>
internal readonly record struct ProjectInfoConfig(bool IsPublic, string? RootNamespace);
