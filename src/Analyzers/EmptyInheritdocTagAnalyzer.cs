using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Analyzes type XML documentation comments and reports a diagnostic
/// when an <c>&lt;inheritdoc/&gt;</c> tag has no attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyInheritdocTagAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML008";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Inheritdoc tag should specify a target",
        messageFormat: "Inheritdoc tag should specify a target (for example, a cref attribute)",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid empty inheritdoc tags by specifying the source documentation target.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.InterfaceDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        SyntaxTriviaList leadingTrivia = typeDeclaration.GetLeadingTrivia();

        foreach (SyntaxTrivia trivia in leadingTrivia)
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                continue;
            }

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax docComment)
                continue;

            foreach (XmlNodeSyntax xmlNode in docComment.Content)
            {
                if (xmlNode is XmlEmptyElementSyntax emptyElement &&
                    emptyElement.Name.LocalName.Text == "inheritdoc" &&
                    emptyElement.Attributes.Count == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, emptyElement.GetLocation()));
                }

                if (xmlNode is XmlElementSyntax element &&
                    element.StartTag?.Name?.LocalName.Text == "inheritdoc" &&
                    element.StartTag.Attributes.Count == 0 &&
                    IsElementContentEmpty(element))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, element.GetLocation()));
                }
            }
        }
    }

    private static bool IsElementContentEmpty(XmlElementSyntax element)
    {
        foreach (XmlNodeSyntax node in element.Content)
        {
            if (node is XmlTextSyntax xmlText)
            {
                foreach (SyntaxToken token in xmlText.TextTokens)
                {
                    if (token.ValueText.Trim().Length > 0)
                        return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}
