using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
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
/// Provides a code fix for <see cref="TaskAsyncResultDocumentationAnalyzer"/> that adds or fills
/// the required <c>&lt;returns&gt;</c> documentation.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskAsyncResultDocumentationFixer))]
[Shared]
public sealed class TaskAsyncResultDocumentationFixer : CodeFixProvider
{
    private static readonly Regex EmptyReturnsTagPattern = new(
        @"<returns\b[^>]*>\s*</returns>|<returns\b[^>]*/>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(TaskAsyncResultDocumentationAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Document async Task return value",
                createChangedDocument: ct => AddOrFillReturnsDocumentationAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(TaskAsyncResultDocumentationFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddOrFillReturnsDocumentationAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        MethodDeclarationSyntax? methodDeclaration = root.FindToken(span.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (methodDeclaration is null)
            return document;

        SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        SyntaxTrivia? docTrivia = GetDocumentationCommentTrivia(methodDeclaration);

        if (docTrivia is SyntaxTrivia existingDocTrivia)
        {
            string existingDocText = existingDocTrivia.ToFullString();
            string updatedDocText = EmptyReturnsTagPattern.Replace(
                existingDocText,
                $"<returns>{TaskAsyncResultDocumentationAnalyzer.ReturnsDocumentationText}</returns>",
                1);

            if (updatedDocText != existingDocText)
            {
                SourceText updatedText = text.WithChanges(
                    new TextChange(existingDocTrivia.FullSpan, updatedDocText));
                return document.WithText(updatedText);
            }
        }

        TextLine methodLine = text.Lines.GetLineFromPosition(methodDeclaration.SpanStart);
        string indentation = text.ToString(TextSpan.FromBounds(methodLine.Start, methodDeclaration.SpanStart));
        string lineBreak = methodLine.EndIncludingLineBreak > methodLine.End
            ? text.ToString(TextSpan.FromBounds(methodLine.End, methodLine.EndIncludingLineBreak))
            : "\n";

        string insertion =
            $"{indentation}/// <returns>{TaskAsyncResultDocumentationAnalyzer.ReturnsDocumentationText}</returns>{lineBreak}";

        SourceText newText = text.WithChanges(new TextChange(new TextSpan(methodLine.Start, 0), insertion));
        return document.WithText(newText);
    }

    private static SyntaxTrivia? GetDocumentationCommentTrivia(MethodDeclarationSyntax methodDeclaration)
    {
        foreach (SyntaxTrivia trivia in methodDeclaration.GetLeadingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                return trivia;
            }
        }

        return null;
    }
}
