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
/// Provides a code fix for <see cref="EmptyInheritdocTagAnalyzer"/> that fills the empty
/// <c>&lt;inheritdoc/&gt;</c> tag with a <c>cref</c> attribute value of "TBD".
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyInheritdocTagFixer))]
[Shared]
public sealed class EmptyInheritdocTagFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(EmptyInheritdocTagAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'cref=\"TBD\"' attribute",
                createChangedDocument: ct => AddCrefAttributeAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(EmptyInheritdocTagFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddCrefAttributeAsync(
        Document document,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        SyntaxNode? node = root.FindNode(span, findInsideTrivia: true);
        XmlCrefAttributeSyntax crefAttribute = CreateCrefAttribute();

        if (node is XmlEmptyElementSyntax emptyElement &&
            emptyElement.Name.LocalName.Text == "inheritdoc")
        {
            XmlEmptyElementSyntax newElement = emptyElement.WithAttributes(emptyElement.Attributes.Add(crefAttribute));
            SyntaxNode newRoot = root.ReplaceNode(emptyElement, newElement);
            return document.WithSyntaxRoot(newRoot);
        }

        if (node is XmlElementSyntax element &&
            element.StartTag?.Name?.LocalName.Text == "inheritdoc")
        {
            XmlElementStartTagSyntax newStartTag = element.StartTag.WithAttributes(element.StartTag.Attributes.Add(crefAttribute));
            XmlElementSyntax newElement = element.WithStartTag(newStartTag);
            SyntaxNode newRoot = root.ReplaceNode(element, newElement);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }

    private static XmlCrefAttributeSyntax CreateCrefAttribute()
    {
        return SyntaxFactory.XmlCrefAttribute(
            SyntaxFactory.XmlName("cref"),
            SyntaxFactory.Token(SyntaxKind.DoubleQuoteToken),
            SyntaxFactory.NameMemberCref(SyntaxFactory.IdentifierName("TBD")),
            SyntaxFactory.Token(SyntaxKind.DoubleQuoteToken))
            .WithLeadingTrivia(SyntaxFactory.Space);
    }
}
