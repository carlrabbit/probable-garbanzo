using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace Analyzers;

/// <summary>
/// Replaces multiple spaces between tokens with a single space.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MultipleSpacesBetweenTokensFixer))]
[Shared]
public sealed class MultipleSpacesBetweenTokensFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MultipleSpacesBetweenTokensAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with single space",
                createChangedDocument: ct => ReplaceWithSingleSpaceAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(MultipleSpacesBetweenTokensFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> ReplaceWithSingleSpaceAsync(
        Document document,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        SyntaxTrivia trivia = root.FindTrivia(span.Start, findInsideTrivia: true);
        if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            return document;

        SyntaxTrivia replacementTrivia = SyntaxFactory.Whitespace(" ");
        SyntaxNode newRoot = root.ReplaceTrivia(trivia, replacementTrivia);
        return document.WithSyntaxRoot(newRoot);
    }
}
