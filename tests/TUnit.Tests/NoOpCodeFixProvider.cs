using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace TUnit.Tests;

/// <summary>
/// A no-op code fix provider used in tests for analyzers that intentionally have no fixer.
/// </summary>
public sealed class NoOpCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray<string>.Empty;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public override FixAllProvider? GetFixAllProvider() => null;
}
