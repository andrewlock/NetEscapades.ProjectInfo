using FluentAssertions;
using Xunit;

namespace NetEscapades.ProjectInfo.Tests;

public class ProjectInfoGeneratorTests
{
    [Fact]
    public void GeneratorDoesNotEmitAnything()
    {
        const string input =
            """
            public class MyClass
            {
            }
            """;

        var (diagnostics, output) = TestHelpers.GetGeneratedOutput([new ProjectInfoGenerator()], new(input));

        diagnostics.Should().BeEmpty();
        output.Should().BeEmpty();
    }
}
