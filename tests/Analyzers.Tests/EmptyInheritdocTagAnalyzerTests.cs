using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="EmptyInheritdocTagAnalyzer"/> and <see cref="EmptyInheritdocTagFixer"/>.
/// </summary>
public class EmptyInheritdocTagAnalyzerTests
    : XunitDiagnosticVerifier<EmptyInheritdocTagAnalyzer, EmptyInheritdocTagFixer>
{
    /// <inheritdoc/>
    public override CSharpTestOptions Options => CSharpTestOptions.Default;

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => EmptyInheritdocTagAnalyzer.Rule;

    [Fact]
    public async Task Diagnostic_WhenInheritdocTagIsEmpty()
    {
        await VerifyDiagnosticAsync("""
            /// [|<inheritdoc/>|]
            public class MyClass { }
            """);
    }

    [Fact]
    public async Task NoDiagnostic_WhenInheritdocTagHasCref()
    {
        await VerifyNoDiagnosticAsync("""
            /// <inheritdoc cref="BaseClass"/>
            public class MyClass { }
            """);
    }

    [Fact]
    public async Task Fix_AddsCrefAttribute()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            /// [|<inheritdoc/>|]
            public class MyClass { }
            """,
            """
            /// <inheritdoc cref="TBD"/>
            public class MyClass { }
            """);
    }
}
