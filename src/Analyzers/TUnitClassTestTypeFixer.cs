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

namespace Analyzers;

/// <summary>
/// Adds or fixes the required <c>Property("TestType", ...)</c> attribute on TUnit test classes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TUnitClassTestTypeFixer))]
[Shared]
public sealed class TUnitClassTestTypeFixer : CodeFixProvider
{
    private const string UnitTestsNamespaceSuffix = ".UnitTests";
    private const string E2ENamespaceSuffix = ".E2E";
    private const string UnitValue = "unit";
    private const string E2EValue = "e2e";
    private const string UnknownValue = "??";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(TUnitClassTestTypeAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add required TestType property",
                createChangedDocument: ct => AddOrFixTestTypePropertyAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(TUnitClassTestTypeFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddOrFixTestTypePropertyAsync(
        Document document,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        ClassDeclarationSyntax? classDeclaration = root.FindToken(span.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            return document;

        string testTypeValue = DetermineTestTypeValue(classDeclaration, semanticModel);

        AttributeSyntax? existingTestTypeAttr = FindTestTypePropertyAttribute(classDeclaration);
        ClassDeclarationSyntax updatedClass;

        if (existingTestTypeAttr is not null)
        {
            updatedClass = classDeclaration.ReplaceNode(
                existingTestTypeAttr,
                CreatePropertyAttribute(TUnitClassTestTypeAnalyzer.TestTypeProperty, testTypeValue));
        }
        else
        {
            updatedClass = AddAttributeToClass(
                classDeclaration,
                CreatePropertyAttribute(TUnitClassTestTypeAnalyzer.TestTypeProperty, testTypeValue));
        }

        if (updatedClass == classDeclaration)
            return document;

        SyntaxNode newRoot = root.ReplaceNode(classDeclaration, updatedClass);
        return document.WithSyntaxRoot(newRoot);
    }

    private static string DetermineTestTypeValue(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return UnknownValue;

        string namespaceName = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        if (namespaceName.EndsWith(UnitTestsNamespaceSuffix, System.StringComparison.Ordinal))
            return UnitValue;

        if (namespaceName.EndsWith(E2ENamespaceSuffix, System.StringComparison.Ordinal))
            return E2EValue;

        return UnknownValue;
    }

    private static AttributeSyntax? FindTestTypePropertyAttribute(ClassDeclarationSyntax classDeclaration)
    {
        foreach (AttributeListSyntax attributeList in classDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count < 1)
                    continue;

                if (attribute.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax propertyLiteral &&
                    propertyLiteral.IsKind(SyntaxKind.StringLiteralExpression) &&
                    propertyLiteral.Token.ValueText == TUnitClassTestTypeAnalyzer.TestTypeProperty)
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    private static AttributeSyntax CreatePropertyAttribute(string propertyName, string propertyValue)
    {
        return SyntaxFactory.Attribute(
            SyntaxFactory.IdentifierName("Property"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(propertyName))),
                    SyntaxFactory.AttributeArgument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(propertyValue))),
                })));
    }

    private static ClassDeclarationSyntax AddAttributeToClass(ClassDeclarationSyntax classDeclaration, AttributeSyntax attribute)
    {
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute));

        if (classDeclaration.AttributeLists.Count == 0)
            return classDeclaration.WithAttributeLists(SyntaxFactory.SingletonList(attributeList));

        return classDeclaration.WithAttributeLists(classDeclaration.AttributeLists.Add(attributeList));
    }
}
