using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Analyzes method XML documentation parameter comment tags and reports a diagnostic
/// when a <c>&lt;param&gt;</c> tag is empty and the corresponding parameter type is
/// <c>IConfiguration</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyParamTagAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML002";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Empty IConfiguration parameter documentation should be filled in",
        messageFormat: "Parameter '{0}' of type IConfiguration has an empty documentation comment",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Empty <param> documentation tags for IConfiguration parameters should have a description.");

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

        if (methodDeclaration.ParameterList.Parameters.Count == 0)
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

                if (element.StartTag?.Name?.LocalName.Text != "param")
                    continue;

                if (!IsElementContentEmpty(element))
                    continue;

                string? paramName = GetParamName(element);
                if (paramName is null)
                    continue;

                ParameterSyntax? parameter = methodDeclaration.ParameterList.Parameters
                    .FirstOrDefault(p => p.Identifier.Text == paramName);

                if (parameter is null)
                    continue;

                if (!IsIConfigurationType(parameter.Type, context.SemanticModel))
                    continue;

                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, element.GetLocation(), paramName));
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

    private static string? GetParamName(XmlElementSyntax element)
    {
        foreach (XmlAttributeSyntax attr in element.StartTag.Attributes)
        {
            if (attr is XmlNameAttributeSyntax nameAttr &&
                nameAttr.Name?.LocalName.Text == "name")
            {
                return nameAttr.Identifier.Identifier.Text;
            }
        }

        return null;
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
