using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Analyzes class XML documentation summary comments and reports a diagnostic
/// when the summary text does not end with a period.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassXmlCommentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML001";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Class XML summary comment should end with a period",
        messageFormat: "Class XML summary comment should end with a period",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Class XML documentation summary comments should end with a period.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        SyntaxTriviaList leadingTrivia = classDeclaration.GetLeadingTrivia();

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
                if (xmlNode is not XmlElementSyntax element)
                    continue;

                if (element.StartTag?.Name?.LocalName.Text != "summary")
                    continue;

                string summaryText = GetSummaryText(element);

                if (summaryText.Length == 0 || summaryText.EndsWith("."))
                    continue;

                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, element.GetLocation()));

                return;
            }
        }
    }

    internal static string GetSummaryText(XmlElementSyntax summaryElement)
    {
        var text = new System.Text.StringBuilder();

        foreach (XmlNodeSyntax node in summaryElement.Content)
        {
            if (node is XmlTextSyntax xmlText)
            {
                foreach (SyntaxToken token in xmlText.TextTokens)
                {
                    text.Append(token.ValueText);
                }
            }
        }

        return text.ToString().Trim();
    }
}
