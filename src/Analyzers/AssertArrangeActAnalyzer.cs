using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Ensures TUnit test methods contain Arrange/Act/Assert comments.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AssertArrangeActAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML007";

    private const string ArrangeComment = "// Arrange";
    private const string ActComment = "// Act";
    private const string AssertComment = "// Assert";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "TUnit tests should include Arrange/Act/Assert comments",
        messageFormat: "TUnit test method should contain single-line comments: {0}",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TUnit test methods should contain // Arrange, // Act and // Assert single-line comments.");

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
        if (methodDeclaration.Body is null ||
            !TUnitTestPropertyConventionAnalyzer.HasTUnitTestAttribute(methodDeclaration, context.SemanticModel))
        {
            return;
        }

        var missingComments = ImmutableArray.CreateBuilder<string>(3);

        if (!ContainsSingleLineComment(methodDeclaration.Body, ArrangeComment))
            missingComments.Add(ArrangeComment);

        if (!ContainsSingleLineComment(methodDeclaration.Body, ActComment))
            missingComments.Add(ActComment);

        if (!ContainsSingleLineComment(methodDeclaration.Body, AssertComment))
            missingComments.Add(AssertComment);

        if (missingComments.Count == 0)
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                methodDeclaration.Identifier.GetLocation(),
                string.Join(", ", missingComments)));
    }

    private static bool ContainsSingleLineComment(BlockSyntax body, string commentText)
    {
        foreach (SyntaxTrivia trivia in body.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
                trivia.ToString().Trim() == commentText)
            {
                return true;
            }
        }

        return false;
    }
}
