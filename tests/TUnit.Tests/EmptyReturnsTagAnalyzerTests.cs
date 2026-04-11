using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="EmptyReturnsTagAnalyzer"/> and <see cref="EmptyReturnsTagFixer"/>.
/// </summary>
public class EmptyReturnsTagAnalyzerTests
    : TUnitDiagnosticVerifier<EmptyReturnsTagAnalyzer, EmptyReturnsTagFixer>
{
    /// <inheritdoc/>
    protected override IEnumerable<MetadataReference> AdditionalReferences =>
        new[]
        {
            MetadataReference.CreateFromFile(
                typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly.Location),
        };

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => EmptyReturnsTagAnalyzer.Rule;

    [Test]
    public async Task Diagnostic_WhenEmptyReturnsTagForIConfigurationReturnType()
    {
        await VerifyDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Gets the configuration.</summary>
                /// [|<returns></returns>|]
                public IConfiguration GetConfiguration() => null!;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenReturnsTagHasContent()
    {
        await VerifyNoDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Gets the configuration.</summary>
                /// <returns>The configuration instance.</returns>
                public IConfiguration GetConfiguration() => null!;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenEmptyReturnsTagForNonIConfigurationReturnType()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Gets a value.</summary>
                /// <returns></returns>
                public string GetValue() => string.Empty;
            }
            """);
    }

    [Test]
    public async Task Fix_AddsTbdDescriptionToEmptyReturnsTag()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Gets the configuration.</summary>
                /// [|<returns></returns>|]
                public IConfiguration GetConfiguration() => null!;
            }
            """,
            """
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Gets the configuration.</summary>
                /// <returns>TBD</returns>
                public IConfiguration GetConfiguration() => null!;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNoDocComment()
    {
        await VerifyNoDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                public IConfiguration GetConfiguration() => null!;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenVoidReturnType()
    {
        await VerifyNoDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Does something.</summary>
                public void DoSomething() { }
            }
            """);
    }
}
