using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="ConsecutiveEmptyLinesAnalyzer"/> and <see cref="ConsecutiveEmptyLinesFixer"/>.
/// </summary>
public class ConsecutiveEmptyLinesAnalyzerTests
    : TUnitDiagnosticVerifier<ConsecutiveEmptyLinesAnalyzer, ConsecutiveEmptyLinesFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => ConsecutiveEmptyLinesAnalyzer.Rule;

    [Test]
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

    [Test]
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

    [Test]
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
