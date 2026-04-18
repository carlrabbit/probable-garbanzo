using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="MultipleSpacesBetweenTokensAnalyzer"/> and <see cref="MultipleSpacesBetweenTokensFixer"/>.
/// </summary>
public class MultipleSpacesBetweenTokensAnalyzerTests
    : XunitDiagnosticVerifier<MultipleSpacesBetweenTokensAnalyzer, MultipleSpacesBetweenTokensFixer>
{
    /// <inheritdoc/>
    public override CSharpTestOptions Options => CSharpTestOptions.Default;

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => MultipleSpacesBetweenTokensAnalyzer.Rule;

    [Fact]
    public async Task Diagnostic_WhenMoreThanOneSpaceBetweenTokens()
    {
        await VerifyDiagnosticAsync("""
            public[|  |]class MyClass
            {
            }
            """);
    }

    [Fact]
    public async Task NoDiagnostic_WhenExtraSpacesAreOnlyIndentation()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                public void Run()
                {
                    int value = 1;
                    _ = value;
                }
            }
            """);
    }

    [Fact]
    public async Task Fix_ReplacesMultipleSpacesWithSingleSpace()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class MyClass
            {
                public void Run()
                {
                    int value =[|  |]1;
                    _ = value;
                }
            }
            """,
            """
            public class MyClass
            {
                public void Run()
                {
                    int value = 1;
                    _ = value;
                }
            }
            """);
    }
}
