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
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

/// <summary>
/// Adds missing Arrange/Act/Assert comments to TUnit test methods.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssertArrangeActFixer))]
[Shared]
public sealed class AssertArrangeActFixer : CodeFixProvider
{
    private const string ArrangeComment = "// Arrange";
    private const string ActComment = "// Act";
    private const string AssertComment = "// Assert";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AssertArrangeActAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add Arrange/Act/Assert comments",
                createChangedDocument: ct => AddCommentsAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(AssertArrangeActFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddCommentsAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        MethodDeclarationSyntax? methodDeclaration = root.FindToken(span.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (methodDeclaration?.Body is null ||
            !TUnitTestPropertyConventionAnalyzer.HasTUnitTestAttribute(methodDeclaration, semanticModel))
        {
            return document;
        }

        var commentsToAdd = ImmutableArray.CreateBuilder<string>(3);
        if (!ContainsSingleLineComment(methodDeclaration.Body, ArrangeComment))
            commentsToAdd.Add(ArrangeComment);
        if (!ContainsSingleLineComment(methodDeclaration.Body, ActComment))
            commentsToAdd.Add(ActComment);
        if (!ContainsSingleLineComment(methodDeclaration.Body, AssertComment))
            commentsToAdd.Add(AssertComment);

        if (commentsToAdd.Count == 0)
            return document;

        SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        string endOfLine = GetEndOfLine(sourceText);
        int insertionPosition = methodDeclaration.Body.Statements.Count > 0
            ? methodDeclaration.Body.Statements[0].GetFirstToken().FullSpan.Start
            : methodDeclaration.Body.CloseBraceToken.FullSpan.Start;
        string indentation = GetIndentation(sourceText, methodDeclaration.Body);

        string insertionText = string.Join(endOfLine, commentsToAdd.Select(comment => $"{indentation}{comment}")) + endOfLine;

        SourceText updatedText = sourceText.WithChanges(
            new TextChange(new TextSpan(insertionPosition, 0), insertionText));
        return document.WithText(updatedText);
    }

    private static bool ContainsSingleLineComment(BlockSyntax body, string commentText)
    {
        foreach (SyntaxTrivia trivia in body.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().Trim() == commentText)
                return true;
        }

        return false;
    }

    private static string GetIndentation(SourceText sourceText, BlockSyntax body)
    {
        if (body.Statements.Count > 0)
        {
            TextLine line = sourceText.Lines.GetLineFromPosition(body.Statements[0].SpanStart);
            return GetLeadingWhitespace(line.ToString());
        }

        TextLine openBraceLine = sourceText.Lines.GetLineFromPosition(body.OpenBraceToken.SpanStart);
        return GetLeadingWhitespace(openBraceLine.ToString()) + "    ";
    }

    private static string GetLeadingWhitespace(string text)
    {
        int index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        return text.Substring(0, index);
    }

    private static string GetEndOfLine(SourceText sourceText)
    {
        foreach (TextLine line in sourceText.Lines)
        {
            if (line.EndIncludingLineBreak > line.End)
                return sourceText.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
        }

        return "\n";
    }
}
