using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Ensures TUnit test classes are decorated with a valid <c>Property("TestType", ...)</c> attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TUnitClassTestTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML014";

    internal const string TestTypeProperty = "TestType";
    private static readonly ImmutableArray<string> ValidTestTypeValues =
        ImmutableArray.Create("unit", "e2e");
    private static readonly ImmutableArray<string> TUnitTestAttributeNames =
        ImmutableArray.Create("Test", "TestAttribute", "TUnit.Test", "TUnit.TestAttribute");
    private static readonly ImmutableArray<string> TUnitPropertyAttributeNames =
        ImmutableArray.Create("Property", "PropertyAttribute", "TUnit.Property", "TUnit.PropertyAttribute");

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "TUnit test classes should define a valid TestType property",
        messageFormat: "TUnit test class '{0}' is missing a valid [Property(\"TestType\", ...)] attribute (expected \"unit\" or \"e2e\")",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TUnit test classes should be decorated with [Property(\"TestType\", \"unit\")] or [Property(\"TestType\", \"e2e\")].");

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

        // Bail out early: syntactic check for any method with a potential TUnit Test attribute name
        if (!HasAnyPotentialTUnitTestMethod(classDeclaration))
            return;

        // Semantic check: confirm at least one method has the actual TUnit TestAttribute
        if (!HasAnyTUnitTestMethod(classDeclaration, context.SemanticModel))
            return;

        // Check if the class has [Property("TestType", "unit")] or [Property("TestType", "e2e")]
        foreach (AttributeListSyntax attributeList in classDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (!IsPotentialTUnitAttributeName(attribute, TUnitPropertyAttributeNames))
                    continue;

                if (!IsTUnitPropertyAttribute(attribute, context.SemanticModel))
                    continue;

                string? propertyName = GetStringArgumentValue(attribute, 0, context.SemanticModel);
                string? propertyValue = GetStringArgumentValue(attribute, 1, context.SemanticModel);

                if (propertyName == TestTypeProperty && IsValidTestTypeValue(propertyValue))
                    return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), classDeclaration.Identifier.Text));
    }

    internal static bool IsValidTestTypeValue(string? value) =>
        value is not null && ValidTestTypeValues.Contains(value);

    internal static string? GetStringArgumentValue(AttributeSyntax attribute, int argumentIndex, SemanticModel semanticModel)
    {
        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count <= argumentIndex)
            return null;

        AttributeArgumentSyntax argument = attribute.ArgumentList.Arguments[argumentIndex];
        Optional<object?> constantValue = semanticModel.GetConstantValue(argument.Expression);
        return constantValue.HasValue ? constantValue.Value as string : null;
    }

    private static bool HasAnyPotentialTUnitTestMethod(ClassDeclarationSyntax classDeclaration)
    {
        foreach (MemberDeclarationSyntax member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax methodDeclaration)
                continue;

            foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attribute in attributeList.Attributes)
                {
                    if (IsPotentialTUnitAttributeName(attribute, TUnitTestAttributeNames))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool HasAnyTUnitTestMethod(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        foreach (MemberDeclarationSyntax member in classDeclaration.Members)
        {
            if (member is not MethodDeclarationSyntax methodDeclaration)
                continue;

            foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attribute in attributeList.Attributes)
                {
                    if (IsTUnitTestAttribute(attribute, semanticModel))
                        return true;
                }
            }
        }

        return false;
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
