using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="MultipleSpacesBetweenTokensAnalyzer"/> and <see cref="MultipleSpacesBetweenTokensFixer"/>.
/// </summary>
public class MultipleSpacesBetweenTokensAnalyzerTests
    : TUnitDiagnosticVerifier<MultipleSpacesBetweenTokensAnalyzer, MultipleSpacesBetweenTokensFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => MultipleSpacesBetweenTokensAnalyzer.Rule;

    [Test]
    public async Task Diagnostic_WhenMoreThanOneSpaceBetweenTokens()
    {
        await VerifyDiagnosticAsync("""
            public[|  |]class MyClass
            {
            }
            """);
    }

    [Test]
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

    [Test]
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
