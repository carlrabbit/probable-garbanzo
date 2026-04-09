using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Testing.CSharp.Xunit;

namespace Roslynator.Testing.CSharp.TUnit;

/// <summary>
/// Represents a verifier for a C# diagnostic that is produced by <see cref="DiagnosticAnalyzer"/>.
/// </summary>
public abstract class TUnitDiagnosticVerifier<TAnalyzer, TFixProvider> : XunitDiagnosticVerifier<TAnalyzer, TFixProvider>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFixProvider : CodeFixProvider, new()
{
    /// <summary>
    /// Initializes a new instance of <see cref="TUnitDiagnosticVerifier{TAnalyzer, TFixProvider}"/>.
    /// </summary>
    protected TUnitDiagnosticVerifier()
    {
    }
}
