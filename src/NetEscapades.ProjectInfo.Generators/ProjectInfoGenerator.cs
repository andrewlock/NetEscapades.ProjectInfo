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
    // States for each entry while validating, see Execute()
    private const byte Registered = 1; // Not emitted, but its name is taken (for duplicate detection)
    private const byte Rejected = 2; // Not emitted, and its name isn't taken

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The analyzer config options change for many reasons, so the first stages only pull out the raw strings
        // (which are cheap to compare), and the more expensive parsing and validation only runs if they change.
        var entries = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
                provider.GlobalOptions.TryGetValue(Constants.ItemsPropertyName, out var items)
                && !string.IsNullOrWhiteSpace(items)
                    ? items
                    : null)
            .Select(static (raw, _) => EntryParser.Parse(raw))
            .WithTrackingName(TrackingNames.ParsedItems);

        var config = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue(Constants.VisibilityPropertyName, out var visibility);
                provider.GlobalOptions.TryGetValue(Constants.RootNamespacePropertyName, out var rootNamespace);
                return (visibility, rootNamespace);
            })
            .Select(static (raw, _) => GetConfig(raw.visibility, raw.rootNamespace))
            .WithTrackingName(TrackingNames.Config);

        var languageVersion = context.ParseOptionsProvider
            .Select(static (options, _) =>
                (options as CSharpParseOptions)?.LanguageVersion.MapSpecifiedToEffectiveVersion() ?? LanguageVersion.Default)
            .WithTrackingName(TrackingNames.LanguageVersion);

        context.RegisterSourceOutput(
            entries.Combine(config).Combine(languageVersion),
            static (spc, source) => Execute(spc, source.Left.Left, source.Left.Right, source.Right));
    }

    private static ProjectInfoConfig GetConfig(string? visibility, string? rootNamespace)
    {
        // Trim() doesn't allocate if there is nothing to trim
        var ns = rootNamespace?.Trim();
        return new ProjectInfoConfig(
            IsPublic: visibility is not null && visibility.Trim().Equals("public", StringComparison.OrdinalIgnoreCase),
            RootNamespace: ns is not null && IsValidNamespace(ns) ? ns : null);
    }

    private static bool IsValidNamespace(ReadOnlySpan<char> value)
    {
        while (true)
        {
            var dot = value.IndexOf('.');
            if (!IsValidIdentifier(dot < 0 ? value : value.Slice(0, dot)))
            {
                return false;
            }

            if (dot < 0)
            {
                return true;
            }

            value = value.Slice(dot + 1);
        }
    }

    private static bool IsValidIdentifier(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty || !SyntaxFacts.IsIdentifierStartCharacter(name[0]))
        {
            return false;
        }

        var maybeKeyword = name[0] >= 'a' && name[0] <= 'z';
        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!SyntaxFacts.IsIdentifierPartCharacter(c))
            {
                return false;
            }

            maybeKeyword &= c >= 'a' && c <= 'z';
        }

        // All C# keywords are lower case ASCII letters, so only allocate a string to look it up in that case
        return !maybeKeyword || SyntaxFacts.GetKeywordKind(name.ToString()) == SyntaxKind.None;
    }

    private static void Execute(
        SourceProductionContext context,
        EquatableArray<ProjectInfoEntry> entries,
        ProjectInfoConfig config,
        LanguageVersion languageVersion)
    {
        var all = entries.AsSpan();
        if (all.IsEmpty)
        {
            return;
        }

        // First pass: validate, report any problems, and record which entries should be emitted.
        // Duplicates are found by comparing with the earlier entries, which is fast for the handful of entries expected
        Span<byte> states = all.Length <= 128 ? stackalloc byte[128] : new byte[all.Length];
        states = states.Slice(0, all.Length);

        var emitCount = 0;
        var capacity = SourceGenerationHelper.HeaderCapacity + (config.RootNamespace?.Length ?? 0);
        for (var i = 0; i < all.Length; i++)
        {
            ref readonly var entry = ref all[i];

            DiagnosticDescriptor? descriptor;
            byte state;
            if (entry.Value is null)
            {
                descriptor = DiagnosticDescriptors.UnsupportedValue;
                state = Rejected;
            }
            else if (entry.Name == Constants.ClassName || !IsValidIdentifier(entry.Name.AsSpan()))
            {
                descriptor = DiagnosticDescriptors.InvalidName;
                state = Rejected;
            }
            else if (IsDuplicate(all, states, i))
            {
                descriptor = DiagnosticDescriptors.DuplicateName;
                state = Rejected;
            }
            else if (entry.Value.Length == 0)
            {
                descriptor = DiagnosticDescriptors.MissingValue;
                state = Registered;
            }
            else if (entry.Kind == ProjectInfoKind.Utf8 && languageVersion < LanguageVersion.CSharp11)
            {
                descriptor = DiagnosticDescriptors.Utf8RequiresCSharp11;
                state = Registered;
            }
            else
            {
                descriptor = null;
                state = SourceGenerationHelper.Emit;
                emitCount++;
                capacity += SourceGenerationHelper.EntryCapacity + entry.Name.Length + entry.Value.Length;
            }

            states[i] = state;
            if (descriptor is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, entry.Name));
            }
        }

        if (emitCount == 0)
        {
            return;
        }

        context.AddSource(
            "ProjectInfo.g.cs",
            SourceText.From(SourceGenerationHelper.Generate(config, all, states, capacity), Encoding.UTF8));
    }

    private static bool IsDuplicate(ReadOnlySpan<ProjectInfoEntry> all, ReadOnlySpan<byte> states, int index)
    {
        var name = all[index].Name;
        for (var i = 0; i < index; i++)
        {
            if (states[i] != Rejected && string.Equals(all[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
