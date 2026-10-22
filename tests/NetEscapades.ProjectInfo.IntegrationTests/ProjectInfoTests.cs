using FluentAssertions;
using Xunit;

namespace NetEscapades.ProjectInfo.IntegrationTests;

public class ProjectInfoTests
{
    [Fact]
    public void StringsAreConstants()
    {
        ProjectInfo.Version.Should().Be("1.2.3");
        ProjectInfo.WithSpecialCharacters.Should().Be("a;b \"quoted\" it's | back\\slash é 😀");
    }

    [Fact]
    public void Utf8ValuesAreReadOnlySpans()
    {
        ProjectInfo.VersionBytes.ToArray().Should().Equal(System.Text.Encoding.UTF8.GetBytes("4.5.6"));
        ProjectInfo.SpecialBytes.ToArray().Should().Equal(System.Text.Encoding.UTF8.GetBytes("é 😀"));
    }
}
