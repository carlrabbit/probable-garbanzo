using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Analyzers;

/// <summary>
/// Provides a code fix for <see cref="EmptyReturnsTagAnalyzer"/> that fills the empty
/// <c>&lt;returns&gt;</c> documentation tag with the text "TBD".
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyReturnsTagFixer))]
[Shared]
public sealed class EmptyReturnsTagFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(EmptyReturnsTagAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'TBD' description",
                createChangedDocument: ct => AddTbdDescriptionAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(EmptyReturnsTagFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddTbdDescriptionAsync(
        Document document,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        SyntaxNode? node = root.FindNode(span, findInsideTrivia: true);
        if (node is not XmlElementSyntax returnsElement)
            return document;

        // Build an XmlText node containing "TBD"
        SyntaxToken tbdToken = SyntaxFactory.XmlTextLiteral(
            SyntaxTriviaList.Empty,
            "TBD",
            "TBD",
            SyntaxTriviaList.Empty);

        XmlTextSyntax tbdTextNode = SyntaxFactory.XmlText(
            SyntaxFactory.TokenList(tbdToken));

        // Replace the content of the returns element with "TBD"
        XmlElementSyntax newReturnsElement = returnsElement
            .WithContent(SyntaxFactory.List<XmlNodeSyntax>(new[] { tbdTextNode }));

        SyntaxNode newRoot = root.ReplaceNode(returnsElement, newReturnsElement);
        return document.WithSyntaxRoot(newRoot);
    }
}
