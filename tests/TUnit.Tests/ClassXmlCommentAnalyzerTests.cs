using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp.TUnit;

namespace TUnit.Tests;

/// <summary>
/// Unit tests for <see cref="ClassXmlCommentAnalyzer"/> and <see cref="ClassXmlCommentFixer"/>.
/// </summary>
public class ClassXmlCommentAnalyzerTests
    : TUnitDiagnosticVerifier<ClassXmlCommentAnalyzer, ClassXmlCommentFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => ClassXmlCommentAnalyzer.Rule;

    [Test]
    public async Task Diagnostic_WhenSummaryDoesNotEndWithPeriod()
    {
        await VerifyDiagnosticAsync("""
            /// [|<summary>This is a class without a period</summary>|]
            public class MyClass { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenSummaryEndsWithPeriod()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>This is a class with a period.</summary>
            public class MyClass { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenSummaryIsEmpty()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary></summary>
            public class MyClass { }
            """);
    }

    [Test]
    public async Task Fix_AddsPeriodAtEndOfSummary()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            /// [|<summary>This is a class without a period</summary>|]
            public class MyClass { }
            """,
            """
            /// <summary>This is a class without a period.</summary>
            public class MyClass { }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenMultiLineSummaryDoesNotEndWithPeriod()
    {
        await VerifyDiagnosticAsync("""
            /// [|<summary>
            /// This is a class without a period
            /// </summary>|]
            public class MyClass { }
            """);
    }

    [Test]
    public async Task Fix_AddsPeriodAtEndOfMultiLineSummary()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            /// [|<summary>
            /// This is a class without a period
            /// </summary>|]
            public class MyClass { }
            """,
            """
            /// <summary>
            /// This is a class without a period.
            /// </summary>
            public class MyClass { }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenNoXmlDocComment()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass { }
            """);
    }
}
