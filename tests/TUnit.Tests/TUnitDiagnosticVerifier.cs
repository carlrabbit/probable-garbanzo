using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace TUnit.Tests;

/// <summary>
/// A base class for TUnit diagnostic verifier tests that wraps
/// <see cref="XunitDiagnosticVerifier{TAnalyzer, TFixer}"/>.
/// </summary>
/// <remarks>
/// The <see cref="Options"/> property is sealed here to prevent subclasses from overriding it
/// with a <c>new</c> keyword, which would cause TUnit's source generator to throw an
/// <see cref="System.Reflection.AmbiguousMatchException"/> when reflecting on the property.
/// Use <see cref="GetTestOptions"/> in subclasses to supply custom test options.
/// </remarks>
/// <typeparam name="TAnalyzer">The analyzer type under test.</typeparam>
/// <typeparam name="TFixer">The code fix provider type under test.</typeparam>
public abstract class TUnitDiagnosticVerifier<TAnalyzer, TFixer>
    : XunitDiagnosticVerifier<TAnalyzer, TFixer>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFixer : CodeFixProvider, new()
{
    /// <summary>
    /// Returns the <see cref="CSharpTestOptions"/> to use for this test class.
    /// Override this method in subclasses to supply custom options such as additional
    /// metadata references.
    /// </summary>
    protected virtual CSharpTestOptions GetTestOptions() => CSharpTestOptions.Default;

    /// <inheritdoc/>
    public sealed override CSharpTestOptions Options => GetTestOptions();
}
