using Microsoft.CodeAnalysis;

namespace NetEscapades.ProjectInfo.Diagnostics;

internal static class DiagnosticDescriptors
{
    private const string Category = "NetEscapades.ProjectInfo";

    public static readonly DiagnosticDescriptor InvalidName = new(
        id: "NEPI001",
        title: "Invalid ProjectInfo name",
        messageFormat: "The ProjectInfo item name '{0}' is not a valid C# identifier, or conflicts with the generated class name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateName = new(
        id: "NEPI002",
        title: "Duplicate ProjectInfo name",
        messageFormat: "The ProjectInfo item name '{0}' is defined more than once",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // NEPI003 (missing value) is no longer used: empty values are emitted as ""

    public static readonly DiagnosticDescriptor UnsupportedValue = new(
        id: "NEPI005",
        title: "Unsupported ProjectInfo value",
        messageFormat: "A ProjectInfo item could not be read because its Value contains a back-quote (`), which is not supported",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Utf8RequiresCSharp11 = new(
        id: "NEPI004",
        title: "ProjectInfoUtf8 requires C# 11",
        messageFormat: "The ProjectInfoUtf8 item '{0}' requires C# 11 or later (UTF-8 string literals)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
