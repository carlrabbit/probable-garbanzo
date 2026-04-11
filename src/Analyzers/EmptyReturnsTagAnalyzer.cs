using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Analyzes method XML documentation return comment tags and reports a diagnostic
/// when a <c>&lt;returns&gt;</c> tag is empty and the method return type is
/// <c>IConfiguration</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyReturnsTagAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML003";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Empty IConfiguration return type documentation should be filled in",
        messageFormat: "Method '{0}' returns IConfiguration but has an empty <returns> documentation comment",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Empty <returns> documentation tags for methods returning IConfiguration should have a description.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        if (!IsIConfigurationType(methodDeclaration.ReturnType, context.SemanticModel))
            return;

        SyntaxTriviaList leadingTrivia = methodDeclaration.GetLeadingTrivia();

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

                if (element.StartTag?.Name?.LocalName.Text != "returns")
                    continue;

                if (!IsElementContentEmpty(element))
                    continue;

                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, element.GetLocation(), methodDeclaration.Identifier.Text));
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

    private static bool IsIConfigurationType(TypeSyntax? typeSyntax, SemanticModel semanticModel)
    {
        if (typeSyntax is null)
            return false;

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(typeSyntax);
        ISymbol? symbol = symbolInfo.Symbol;

        if (symbol is ITypeSymbol typeSymbol)
        {
            return IsIConfigurationTypeSymbol(typeSymbol);
        }

        // Fallback: check the type name textually when the symbol is not available
        // (e.g., IConfiguration is not referenced in the compilation).
        return typeSyntax.ToString() == "IConfiguration";
    }

    private static bool IsIConfigurationTypeSymbol(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.Name == "IConfiguration" &&
            typeSymbol.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Configuration")
        {
            return true;
        }

        foreach (INamedTypeSymbol iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name == "IConfiguration" &&
                iface.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Configuration")
            {
                return true;
            }
        }

        return false;
    }
}
