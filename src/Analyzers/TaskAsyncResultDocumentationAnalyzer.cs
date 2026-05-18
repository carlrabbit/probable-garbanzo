using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Ensures that non-private methods returning <see cref="System.Threading.Tasks.Task"/> and ending in
/// <c>Async</c> have a non-empty <c>&lt;returns&gt;</c> XML documentation tag.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskAsyncResultDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML015";

    /// <summary>The required documentation text for matching async task methods.</summary>
    public const string ReturnsDocumentationText = "A task that represents the asynchronous save operation.";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Task Async methods should document their return value",
        messageFormat: "Method '{0}' must have a non-empty <returns> XML documentation comment",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Non-private methods returning Task and ending with Async should document the task they return.");

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

        if (!methodDeclaration.Identifier.ValueText.EndsWith("Async", StringComparison.Ordinal))
            return;

        if (HasExplicitPrivateModifier(methodDeclaration))
            return;

        IMethodSymbol? methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol?.DeclaredAccessibility == Accessibility.Private)
            return;

        if (!IsNonGenericTask(methodSymbol?.ReturnType))
            return;

        if (HasNonEmptyReturnsTag(methodDeclaration))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text));
    }

    private static bool HasExplicitPrivateModifier(MethodDeclarationSyntax methodDeclaration)
    {
        foreach (SyntaxToken modifier in methodDeclaration.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PrivateKeyword))
                return true;
        }

        return false;
    }

    private static bool HasNonEmptyReturnsTag(MethodDeclarationSyntax methodDeclaration)
    {
        foreach (SyntaxTrivia trivia in methodDeclaration.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                continue;
            }

            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax docComment)
                continue;

            foreach (XmlNodeSyntax node in docComment.Content)
            {
                if (node is XmlElementSyntax element &&
                    element.StartTag?.Name?.LocalName.Text == "returns")
                {
                    return !IsElementContentEmpty(element);
                }

                if (node is XmlEmptyElementSyntax emptyElement &&
                    emptyElement.Name.LocalName.Text == "returns")
                {
                    return false;
                }
            }

            return false;
        }

        return false;
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

                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsNonGenericTask(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol { IsGenericType: false } namedTypeSymbol)
            return false;

        return namedTypeSymbol.Name == "Task" &&
               namedTypeSymbol.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";
    }
}
