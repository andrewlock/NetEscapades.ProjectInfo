using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static NetEscapades.ProjectInfo.Tests.ProjectInfoItems;

namespace NetEscapades.ProjectInfo.Tests;

public class DiagnosticTests
{
    private static (string[] Ids, string Output) Run(string[] items, LanguageVersion languageVersion = LanguageVersion.Default)
        => Run(Config(items), languageVersion);

    private static (string[] Ids, string Output) Run(
        Dictionary<string, string> config, LanguageVersion languageVersion = LanguageVersion.Default)
    {
        var options = new TestHelpers.Options(languageVersion, config, null!, ["public class MyClass { }"], null);

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput([new ProjectInfoGenerator()], options);
        return (diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.Id).ToArray(), output);
    }

    [Theory]
    [InlineData("1Bad")]
    [InlineData("has space")]
    [InlineData("class")]
    [InlineData("ProjectInfo")]
    public void InvalidName_ReportsNEPI001(string name)
    {
        var (ids, output) = Run([S(name, "value")]);

        ids.Should().Equal("NEPI001");
        output.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateName_ReportsNEPI002_AndKeepsFirst()
    {
        var (ids, output) = Run([S("Version", "1.0"), U("Version", "2.0")]);

        ids.Should().Equal("NEPI002");
        output.Should().Contain("""const string Version = "1.0";""").And.NotContain("2.0");
    }

    [Fact]
    public void MissingValue_ReportsNEPI003()
    {
        var (ids, output) = Run([S("Version", "")]);

        ids.Should().Equal("NEPI003");
        output.Should().BeEmpty();
    }

    [Fact]
    public void Utf8WithOldLanguageVersion_ReportsNEPI004_AndKeepsStrings()
    {
        var (ids, output) = Run([S("Version", "1.0"), U("Bytes", "2.0")], LanguageVersion.CSharp10);

        ids.Should().Equal("NEPI004");
        output.Should().Contain("const string Version").And.NotContain("Bytes");
    }

    [Fact]
    public void UnevaluatedValue_ReportsNEPI005_AndKeepsOthers()
    {
        // What MSBuild leaves behind when a value contains a back-quote
        const string unevaluated = "S|Bad|$([System.Uri]::EscapeDataString(`a`b`))";
        var config = new Dictionary<string, string> { [ItemsKey] = S("Good", "y") + "," + unevaluated };

        var (ids, output) = Run(config);

        ids.Should().Equal("NEPI005");
        output.Should().Contain("""const string Good = "y";""").And.NotContain("Bad");
    }

    [Fact]
    public void InvalidItemsDoNotPreventValidItems()
    {
        var (ids, output) = Run([S("1Bad", "x"), S("Good", "y"), S("Good", "z"), S("Empty", "")]);

        ids.Should().BeEquivalentTo("NEPI001", "NEPI002", "NEPI003");
        output.Should().Contain("""const string Good = "y";""");
    }
}
