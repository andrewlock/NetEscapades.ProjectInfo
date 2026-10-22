using Microsoft.CodeAnalysis;

namespace NetEscapades.ProjectInfo;

[Generator]
public class ProjectInfoGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The generator is registered but does not emit anything yet
    }
}
