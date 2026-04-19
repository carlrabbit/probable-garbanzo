using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

/// <summary>
/// Removes empty lines reported by <see cref="ConsecutiveEmptyLinesAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConsecutiveEmptyLinesFixer))]
[Shared]
public sealed class ConsecutiveEmptyLinesFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(ConsecutiveEmptyLinesAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove extra empty line",
                createChangedDocument: ct => RemoveExtraEmptyLineAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(ConsecutiveEmptyLinesFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> RemoveExtraEmptyLineAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        int lineIndex = sourceText.Lines.IndexOf(span.Start);
        if (lineIndex < 0)
            return document;

        TextLine line = sourceText.Lines[lineIndex];
        SourceText newText = sourceText.WithChanges(new TextChange(line.SpanIncludingLineBreak, string.Empty));
        return document.WithText(newText);
    }
}
