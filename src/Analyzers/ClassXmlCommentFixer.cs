using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Analyzers;

/// <summary>
/// Provides a code fix for <see cref="ClassXmlCommentAnalyzer"/> that appends a period
/// to the end of the class XML summary comment.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ClassXmlCommentFixer))]
[Shared]
public sealed class ClassXmlCommentFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(ClassXmlCommentAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add period at end of summary",
                createChangedDocument: ct => AddPeriodAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(ClassXmlCommentFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddPeriodAsync(
        Document document,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        SyntaxNode? node = root.FindNode(span, findInsideTrivia: true);
        if (node is not XmlElementSyntax summaryElement)
            return document;

        // Find the last XmlTextSyntax in the content that has non-whitespace text.
        XmlTextSyntax? lastTextNode = null;
        int lastNonWhitespaceTokenIndex = -1;

        for (int i = summaryElement.Content.Count - 1; i >= 0; i--)
        {
            if (summaryElement.Content[i] is not XmlTextSyntax xmlText)
                continue;

            for (int j = xmlText.TextTokens.Count - 1; j >= 0; j--)
            {
                string value = xmlText.TextTokens[j].ValueText;
                if (value.Trim().Length > 0)
                {
                    lastTextNode = xmlText;
                    lastNonWhitespaceTokenIndex = j;
                    break;
                }
            }

            if (lastTextNode is not null)
                break;
        }

        if (lastTextNode is null || lastNonWhitespaceTokenIndex < 0)
            return document;

        SyntaxToken tokenToModify = lastTextNode.TextTokens[lastNonWhitespaceTokenIndex];
        string newText = tokenToModify.ValueText + ".";
        SyntaxToken newToken = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.XmlTextLiteral(
            tokenToModify.LeadingTrivia,
            newText,
            newText,
            tokenToModify.TrailingTrivia);

        SyntaxTokenList newTokenList = lastTextNode.TextTokens.Replace(tokenToModify, newToken);
        XmlTextSyntax newTextNode = lastTextNode.WithTextTokens(newTokenList);
        XmlElementSyntax newSummaryElement = summaryElement.ReplaceNode(lastTextNode, newTextNode);

        SyntaxNode newRoot = root.ReplaceNode(summaryElement, newSummaryElement);
        return document.WithSyntaxRoot(newRoot);
    }
}
