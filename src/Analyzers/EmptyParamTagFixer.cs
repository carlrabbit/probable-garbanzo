using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Analyzers;

/// <summary>
/// Provides a code fix for <see cref="EmptyParamTagAnalyzer"/> that fills the empty
/// <c>&lt;param&gt;</c> documentation tag with the text "TBD".
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyParamTagFixer))]
[Shared]
public sealed class EmptyParamTagFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(EmptyParamTagAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'TBD' description",
                createChangedDocument: ct => AddTbdDescriptionAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(EmptyParamTagFixer)),
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
        if (node is not XmlElementSyntax paramElement)
            return document;

        // Build an XmlText node containing "TBD"
        SyntaxToken tbdToken = SyntaxFactory.XmlTextLiteral(
            SyntaxTriviaList.Empty,
            "TBD",
            "TBD",
            SyntaxTriviaList.Empty);

        XmlTextSyntax tbdTextNode = SyntaxFactory.XmlText(
            SyntaxFactory.TokenList(tbdToken));

        // Replace the content of the param element with "TBD"
        XmlElementSyntax newParamElement = paramElement
            .WithContent(SyntaxFactory.List<XmlNodeSyntax>(new[] { tbdTextNode }));

        SyntaxNode newRoot = root.ReplaceNode(paramElement, newParamElement);
        return document.WithSyntaxRoot(newRoot);
    }
}
