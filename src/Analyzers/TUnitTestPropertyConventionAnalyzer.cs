using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Ensures TUnit test methods define required property annotations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TUnitTestPropertyConventionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML004";

    private const string TestTypeProperty = "TestType";
    private const string TestTargetSpecProperty = "TestTargetSpec";
    private static readonly ImmutableArray<string> TUnitTestAttributeNames =
        ImmutableArray.Create("Test", "TestAttribute", "TUnit.Test", "TUnit.TestAttribute");
    private static readonly ImmutableArray<string> TUnitPropertyAttributeNames =
        ImmutableArray.Create("Property", "PropertyAttribute", "TUnit.Property", "TUnit.PropertyAttribute");

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "TUnit tests should define required properties",
        messageFormat: "TUnit test method should define valid properties: {0}",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TUnit test methods should define Property attributes for TestType and TestTargetSpec.");

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
        if (!HasTUnitTestAttribute(methodDeclaration, context.SemanticModel))
            return;

        bool hasValidTestType = false;
        bool hasValidTargetSpec = false;

        foreach (AttributeSyntax propertyAttribute in GetTUnitPropertyAttributes(methodDeclaration, context.SemanticModel))
        {
            string? propertyName = GetStringArgumentValue(propertyAttribute, 0, context.SemanticModel);
            string? propertyValue = GetStringArgumentValue(propertyAttribute, 1, context.SemanticModel);

            if (propertyName == TestTypeProperty && IsValidTestType(propertyValue))
                hasValidTestType = true;

            if (propertyName == TestTargetSpecProperty && !string.IsNullOrWhiteSpace(propertyValue))
                hasValidTargetSpec = true;
        }

        if (hasValidTestType && hasValidTargetSpec)
            return;

        string missingProperties = hasValidTestType
            ? TestTargetSpecProperty
            : hasValidTargetSpec
                ? TestTypeProperty
                : $"{TestTypeProperty}, {TestTargetSpecProperty}";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), missingProperties));
    }

    internal static bool HasTUnitTestAttribute(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
    {
        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (IsTUnitTestAttribute(attribute, semanticModel))
                    return true;
            }
        }

        return false;
    }

    internal static ImmutableArray<AttributeSyntax> GetTUnitPropertyAttributes(MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
    {
        var builder = ImmutableArray.CreateBuilder<AttributeSyntax>();

        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (IsTUnitPropertyAttribute(attribute, semanticModel))
                    builder.Add(attribute);
            }
        }

        return builder.ToImmutable();
    }

    internal static bool IsValidTestType(string? value) =>
        value == "UnitTest" || value == "IntegrationTest";

    internal static string? GetStringArgumentValue(AttributeSyntax attribute, int argumentIndex, SemanticModel semanticModel)
    {
        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count <= argumentIndex)
            return null;

        AttributeArgumentSyntax argument = attribute.ArgumentList.Arguments[argumentIndex];
        Optional<object?> constantValue = semanticModel.GetConstantValue(argument.Expression);
        return constantValue.HasValue ? constantValue.Value as string : null;
    }

    private static bool IsTUnitTestAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        if (!IsPotentialTUnitAttributeName(attribute, TUnitTestAttributeNames))
            return false;

        if (semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol symbol)
        {
            INamedTypeSymbol attributeType = symbol.ContainingType;
            return attributeType.Name == "TestAttribute" &&
                   attributeType.ContainingNamespace.ToDisplayString().StartsWith("TUnit");
        }

        return true;
    }

    private static bool IsTUnitPropertyAttribute(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        if (!IsPotentialTUnitAttributeName(attribute, TUnitPropertyAttributeNames))
            return false;

        if (semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol symbol)
        {
            INamedTypeSymbol attributeType = symbol.ContainingType;
            return attributeType.Name == "PropertyAttribute" &&
                   attributeType.ContainingNamespace.ToDisplayString().StartsWith("TUnit");
        }

        return true;
    }

    private static bool IsPotentialTUnitAttributeName(
        AttributeSyntax attribute,
        ImmutableArray<string> allowedNames)
    {
        string name = attribute.Name.ToString();

        foreach (string allowedName in allowedNames)
        {
            if (name == allowedName)
                return true;
        }

        return false;
    }
}
