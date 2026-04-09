using Microsoft.CodeAnalysis.CodeFixes;
using Roslynator.Testing.CSharp.Xunit;

namespace Roslynator.Testing.CSharp.TUnit;

/// <summary>
/// Represents a verifier for C# compiler diagnostics.
/// </summary>
public abstract class TUnitCompilerDiagnosticFixVerifier<TFixProvider> : XunitCompilerDiagnosticFixVerifier<TFixProvider>
    where TFixProvider : CodeFixProvider, new()
{
    /// <summary>
    /// Initializes a new instance of <see cref="TUnitCompilerDiagnosticFixVerifier{TFixProvider}"/>.
    /// </summary>
    protected TUnitCompilerDiagnosticFixVerifier()
    {
    }
}
