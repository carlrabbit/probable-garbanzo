using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="EmptyReturnsTagAnalyzer"/> and <see cref="EmptyReturnsTagFixer"/>.
/// </summary>
public class EmptyReturnsTagAnalyzerTests
    : XunitDiagnosticVerifier<EmptyReturnsTagAnalyzer, EmptyReturnsTagFixer>
{
    /// <inheritdoc/>
    public override CSharpTestOptions Options => CSharpTestOptions.Default
        .WithMetadataReferences(
            CSharpTestOptions.Default.MetadataReferences.Add(
                MetadataReference.CreateFromFile(
                    typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly.Location)));

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => EmptyReturnsTagAnalyzer.Rule;

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
