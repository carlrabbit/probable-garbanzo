using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="ConsecutiveEmptyLinesAnalyzer"/> and <see cref="ConsecutiveEmptyLinesFixer"/>.
/// </summary>
public class ConsecutiveEmptyLinesAnalyzerTests
    : XunitDiagnosticVerifier<ConsecutiveEmptyLinesAnalyzer, ConsecutiveEmptyLinesFixer>
{
    /// <inheritdoc/>
    public override CSharpTestOptions Options => CSharpTestOptions.Default;

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => ConsecutiveEmptyLinesAnalyzer.Rule;

    [Fact]
    public async Task Diagnostic_WhenMoreThanOneConsecutiveEmptyLine()
    {
        await VerifyDiagnosticAsync("""
            public class FirstClass
            {
            }

            [|    |]
            public class SecondClass
            {
            }
            """);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSingleEmptyLineBetweenMembers()
    {
        await VerifyNoDiagnosticAsync("""
            public class FirstClass
            {
            }

            public class SecondClass
            {
            }
            """);
    }

    [Fact]
    public async Task Fix_RemovesExtraEmptyLine()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class FirstClass
            {
            }

            [|    |]
            public class SecondClass
            {
            }
            """,
            """
            public class FirstClass
            {
            }

            public class SecondClass
            {
            }
            """);
    }
}
