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
/// Adds or fixes required TUnit test property annotations.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TUnitTestPropertyConventionFixer))]
[Shared]
public sealed class TUnitTestPropertyConventionFixer : CodeFixProvider
{
    private const string TestTypeProperty = "TestType";
    private const string TestTargetSpecProperty = "TestTargetSpec";
    private const string DefaultValue = "TBD";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(TUnitTestPropertyConventionAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add required TUnit properties",
                createChangedDocument: ct => AddOrFixPropertiesAsync(context.Document, context.Span, ct),
                equivalenceKey: nameof(TUnitTestPropertyConventionFixer)),
            context.Diagnostics);

        return Task.CompletedTask;
    }

    private static async Task<Document> AddOrFixPropertiesAsync(
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

        MethodDeclarationSyntax? methodDeclaration = root.FindToken(span.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (methodDeclaration is null || !TUnitTestPropertyConventionAnalyzer.HasTUnitTestAttribute(methodDeclaration, semanticModel))
            return document;

        string testTypeValue = GetDefaultPropertyValue(methodDeclaration, semanticModel, TestTypeProperty) ?? DefaultValue;
        string targetSpecValue = GetDefaultPropertyValue(methodDeclaration, semanticModel, TestTargetSpecProperty) ?? DefaultValue;

        bool hasValidTestType = false;
        bool hasValidTargetSpec = false;

        foreach (AttributeSyntax attribute in TUnitTestPropertyConventionAnalyzer.GetTUnitPropertyAttributes(methodDeclaration, semanticModel))
        {
            string? propertyName = TUnitTestPropertyConventionAnalyzer.GetStringArgumentValue(attribute, 0, semanticModel);
            string? propertyValue = TUnitTestPropertyConventionAnalyzer.GetStringArgumentValue(attribute, 1, semanticModel);

            if (propertyName == TestTypeProperty)
            {
                if (TUnitTestPropertyConventionAnalyzer.IsValidTestType(propertyValue))
                    hasValidTestType = true;
            }

            if (propertyName == TestTargetSpecProperty)
            {
                if (!string.IsNullOrWhiteSpace(propertyValue))
                    hasValidTargetSpec = true;
            }
        }

        MethodDeclarationSyntax updatedMethod = methodDeclaration;

        if (!hasValidTestType)
        {
            AttributeSyntax? currentTestType = FindPropertyAttribute(updatedMethod, TestTypeProperty);
            if (currentTestType is not null)
            {
                updatedMethod = updatedMethod.ReplaceNode(currentTestType, CreatePropertyAttribute(TestTypeProperty, testTypeValue));
            }
            else
            {
                updatedMethod = AddAttribute(updatedMethod, CreatePropertyAttribute(TestTypeProperty, testTypeValue));
            }
        }

        if (!hasValidTargetSpec)
        {
            AttributeSyntax? currentTargetSpec = FindPropertyAttribute(updatedMethod, TestTargetSpecProperty);
            if (currentTargetSpec is not null)
            {
                updatedMethod = updatedMethod.ReplaceNode(currentTargetSpec, CreatePropertyAttribute(TestTargetSpecProperty, targetSpecValue));
            }
            else
            {
                updatedMethod = AddAttribute(updatedMethod, CreatePropertyAttribute(TestTargetSpecProperty, targetSpecValue));
            }
        }

        if (updatedMethod == methodDeclaration)
            return document;

        SyntaxNode newRoot = root.ReplaceNode(methodDeclaration, updatedMethod);
        return document.WithSyntaxRoot(newRoot);
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

    private static MethodDeclarationSyntax AddAttribute(MethodDeclarationSyntax methodDeclaration, AttributeSyntax attribute)
    {
        AttributeListSyntax attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(attribute));

        if (methodDeclaration.AttributeLists.Count == 0)
            return methodDeclaration.WithAttributeLists(SyntaxFactory.SingletonList(attributeList));

        return methodDeclaration.WithAttributeLists(methodDeclaration.AttributeLists.Add(attributeList));
    }

    private static string? GetDefaultPropertyValue(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, string propertyName)
    {
        if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol methodSymbol)
            return null;

        for (INamedTypeSymbol? typeSymbol = methodSymbol.ContainingType;
             typeSymbol is not null;
             typeSymbol = typeSymbol.BaseType)
        {
            foreach (AttributeData attributeData in typeSymbol.GetAttributes())
            {
                if (!IsTUnitDefaultPropertyAttribute(attributeData.AttributeClass))
                    continue;

                if (attributeData.ConstructorArguments.Length < 2)
                    continue;

                string? attrProperty = attributeData.ConstructorArguments[0].Value as string;
                string? attrValue = attributeData.ConstructorArguments[1].Value as string;

                if (attrProperty == propertyName && !string.IsNullOrEmpty(attrValue))
                    return attrValue;
            }
        }

        return null;
    }

    private static bool IsTUnitDefaultPropertyAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
            return false;

        return attributeClass.Name == nameof(TUnitDefaultPropertyAttribute) ||
               attributeClass.ToDisplayString() == "Analyzers.TUnitDefaultPropertyAttribute";
    }

    private static AttributeSyntax? FindPropertyAttribute(
        MethodDeclarationSyntax methodDeclaration,
        string propertyName)
    {
        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count < 2)
                    continue;

                if (attribute.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax propertyLiteral ||
                    !propertyLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    continue;
                }

                if (propertyLiteral.Token.ValueText == propertyName)
                    return attribute;
            }
        }

        return null;
    }
}
