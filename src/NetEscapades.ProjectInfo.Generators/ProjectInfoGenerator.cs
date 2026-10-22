using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NetEscapades.ProjectInfo.Diagnostics;

namespace NetEscapades.ProjectInfo;

[Generator]
public class ProjectInfoGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entries = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                provider.GlobalOptions.TryGetValue(Constants.ItemsPropertyName, out var items)
                && !string.IsNullOrWhiteSpace(items)
                    ? items
                    : null)
            .Select(static (raw, _) => GetEntries(raw))
            .WithTrackingName(TrackingNames.ParsedItems);

        var config = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GetConfig(provider))
            .WithTrackingName(TrackingNames.Config);

        var languageVersion = context.ParseOptionsProvider
            .Select(static (options, _) =>
                (options as CSharpParseOptions)?.LanguageVersion.MapSpecifiedToEffectiveVersion() ?? LanguageVersion.Default)
            .WithTrackingName(TrackingNames.LanguageVersion);

        context.RegisterSourceOutput(
            entries.Combine(config).Combine(languageVersion),
            static (spc, source) => Execute(spc, source.Left.Left, source.Left.Right, source.Right));
    }

    /// <summary>
    /// Parses the ProjectInfoItems property, a comma-separated list of "S|name|value" or "U|name|value" entries
    /// in which the name and value are URI-escaped. See the .targets file.
    /// </summary>
    private static EquatableArray<ProjectInfoEntry> GetEntries(string? projectInfoEntries)
    {
        if (projectInfoEntries is null)
        {
            return default;
        }

        var entries = new List<ProjectInfoEntry>();
        foreach (var item in projectInfoEntries.Split([','], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('|');

            // Correctly escaped text never contains '$', so this means MSBuild didn't evaluate it (the value contained a back-quote)
            if (parts.Length == 3
                && (parts[0] == "S" || parts[0] == "U")
                && parts[1].IndexOf('$') < 0
                && parts[2].IndexOf('$') < 0)
            {
                entries.Add(new ProjectInfoEntry(
                    parts[0] == "S" ? ProjectInfoKind.String : ProjectInfoKind.Utf8,
                    Uri.UnescapeDataString(parts[1]),
                    Uri.UnescapeDataString(parts[2])));
            }
            else
            {
                entries.Add(new ProjectInfoEntry(ProjectInfoKind.String, string.Empty, null));
            }
        }

        return new EquatableArray<ProjectInfoEntry>(entries.ToArray());
    }

    private static ProjectInfoConfig GetConfig(AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GlobalOptions;
        options.TryGetValue(Constants.VisibilityPropertyName, out var visibility);
        options.TryGetValue(Constants.RootNamespacePropertyName, out var rootNamespace);

        return new ProjectInfoConfig(
            IsPublic: string.Equals(visibility?.Trim(), "public", StringComparison.OrdinalIgnoreCase),
            RootNamespace: IsValidNamespace(rootNamespace) ? rootNamespace!.Trim() : null);
    }

    private static bool IsValidNamespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var part in value!.Trim().Split('.'))
        {
            if (!IsValidIdentifier(part))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidIdentifier(string name)
        => SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    private static void Execute(
        SourceProductionContext context,
        EquatableArray<ProjectInfoEntry> entries,
        ProjectInfoConfig config,
        LanguageVersion languageVersion)
    {
        var valid = new List<ProjectInfoEntry>(entries.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var descriptor =
                entry.Value is null ? DiagnosticDescriptors.UnsupportedValue
                : !IsValidIdentifier(entry.Name) || entry.Name == Constants.ClassName ? DiagnosticDescriptors.InvalidName
                : !seen.Add(entry.Name) ? DiagnosticDescriptors.DuplicateName
                : entry.Value.Length == 0 ? DiagnosticDescriptors.MissingValue
                : entry.Kind == ProjectInfoKind.Utf8 && languageVersion < LanguageVersion.CSharp11 ? DiagnosticDescriptors.Utf8RequiresCSharp11
                : null;

            if (descriptor is null)
            {
                valid.Add(entry);
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, entry.Name));
            }
        }

        if (valid.Count == 0)
        {
            return;
        }

        context.AddSource("ProjectInfo.g.cs", SourceText.From(SourceGenerationHelper.Generate(config, valid), Encoding.UTF8));
    }
}
