using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

/// <summary>
/// Ensures that non-private user-defined types and members have complete XML documentation comments,
/// including non-empty summary, returns (when applicable), param (per parameter), and typeparam (per type parameter) tags.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WholesomeCommentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for a missing or empty summary tag.</summary>
    public const string MissingSummaryDiagnosticId = "XML010";

    /// <summary>The diagnostic ID for a missing or empty returns tag.</summary>
    public const string MissingReturnsDiagnosticId = "XML011";

    /// <summary>The diagnostic ID for a missing or empty param tag.</summary>
    public const string MissingParamDiagnosticId = "XML012";

    /// <summary>The diagnostic ID for a missing or empty typeparam tag.</summary>
    public const string MissingTypeParamDiagnosticId = "XML013";

    /// <summary>Diagnostic rule for a missing or empty XML summary documentation tag.</summary>
    public static readonly DiagnosticDescriptor MissingSummaryRule = new(
        id: MissingSummaryDiagnosticId,
        title: "Non-private member is missing a non-empty XML summary comment",
        messageFormat: "'{0}' is missing a non-empty <summary> XML documentation comment",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All non-private types and members should have a non-empty <summary> XML documentation comment.");

    /// <summary>Diagnostic rule for a missing or empty XML returns documentation tag.</summary>
    public static readonly DiagnosticDescriptor MissingReturnsRule = new(
        id: MissingReturnsDiagnosticId,
        title: "Non-private member is missing a non-empty XML returns comment",
        messageFormat: "'{0}' is missing a non-empty <returns> XML documentation comment",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Non-private methods and properties with a non-void return type should have a non-empty <returns> XML documentation comment.");

    /// <summary>Diagnostic rule for a missing or empty XML param documentation tag.</summary>
    public static readonly DiagnosticDescriptor MissingParamRule = new(
        id: MissingParamDiagnosticId,
        title: "Non-private member is missing a non-empty XML param comment",
        messageFormat: "'{0}' is missing a non-empty <param> XML documentation comment for parameter '{1}'",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All parameters of non-private members should have a non-empty <param> XML documentation comment.");

    /// <summary>Diagnostic rule for a missing or empty XML typeparam documentation tag.</summary>
    public static readonly DiagnosticDescriptor MissingTypeParamRule = new(
        id: MissingTypeParamDiagnosticId,
        title: "Non-private member is missing a non-empty XML typeparam comment",
        messageFormat: "'{0}' is missing a non-empty <typeparam> XML documentation comment for type parameter '{1}'",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All type parameters of non-private members and types should have a non-empty <typeparam> XML documentation comment.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingSummaryRule, MissingReturnsRule, MissingParamRule, MissingTypeParamRule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Type declarations
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InterfaceDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.RecordDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.RecordStructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EnumDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.DelegateDeclaration);

        // Member declarations
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EventDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EventFieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.IndexerDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EnumMemberDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        SyntaxNode node = context.Node;

        // Quick syntactic bail-out for explicitly private members
        if (HasExplicitPrivateModifier(node))
            return;

        // Check effective accessibility via semantic model (handles default-private class members, etc.)
        if (IsEffectivelyPrivate(context.SemanticModel, node))
            return;

        DocumentationCommentTriviaSyntax? docComment = GetDocComment(node);

        // Members covered by inheritdoc inherit their documentation; skip them
        if (docComment != null && HasInheritdoc(docComment))
            return;

        string memberName = GetMemberName(node);
        Location nameLocation = GetNameLocation(node);

        // XML010: every non-private member needs a non-empty <summary>
        if (!HasNonEmptyTag(docComment, "summary"))
            context.ReportDiagnostic(Diagnostic.Create(MissingSummaryRule, nameLocation, memberName));

        // The following checks only apply when a doc comment is already present.
        // If there is no doc comment at all, the summary diagnostic above is sufficient.
        if (docComment is null)
            return;

        // XML011: non-void/non-Task returning members need a non-empty <returns>
        if (RequiresReturnsTag(node, context.SemanticModel) && !HasNonEmptyTag(docComment, "returns"))
            context.ReportDiagnostic(Diagnostic.Create(MissingReturnsRule, nameLocation, memberName));

        // XML012: each parameter needs a non-empty <param name="..."> tag
        SeparatedSyntaxList<ParameterSyntax> parameters = GetParameters(node);
        foreach (ParameterSyntax param in parameters)
        {
            string paramName = param.Identifier.Text;
            if (!HasNonEmptyNamedTag(docComment, "param", paramName))
                context.ReportDiagnostic(Diagnostic.Create(MissingParamRule, param.Identifier.GetLocation(), memberName, paramName));
        }

        // XML013: each type parameter needs a non-empty <typeparam name="..."> tag
        SeparatedSyntaxList<TypeParameterSyntax> typeParameters = GetTypeParameters(node);
        foreach (TypeParameterSyntax typeParam in typeParameters)
        {
            string typeParamName = typeParam.Identifier.Text;
            if (!HasNonEmptyNamedTag(docComment, "typeparam", typeParamName))
                context.ReportDiagnostic(Diagnostic.Create(MissingTypeParamRule, typeParam.Identifier.GetLocation(), memberName, typeParamName));
        }
    }

    private static bool HasExplicitPrivateModifier(SyntaxNode node)
    {
        if (node is MemberDeclarationSyntax memberDecl)
        {
            foreach (SyntaxToken modifier in memberDecl.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PrivateKeyword))
                    return true;
            }
        }

        return false;
    }

    private static bool IsEffectivelyPrivate(SemanticModel semanticModel, SyntaxNode node)
    {
        ISymbol? symbol = GetDeclaredSymbol(semanticModel, node);
        return symbol?.DeclaredAccessibility == Accessibility.Private;
    }

    private static ISymbol? GetDeclaredSymbol(SemanticModel semanticModel, SyntaxNode node)
    {
        // FieldDeclarationSyntax and EventFieldDeclarationSyntax do not have a single symbol;
        // use the first variable declarator to get the field symbol for accessibility.
        if (node is FieldDeclarationSyntax fieldDecl)
        {
            foreach (VariableDeclaratorSyntax variable in fieldDecl.Declaration.Variables)
                return semanticModel.GetDeclaredSymbol(variable);
            return null;
        }

        if (node is EventFieldDeclarationSyntax eventFieldDecl)
        {
            foreach (VariableDeclaratorSyntax variable in eventFieldDecl.Declaration.Variables)
                return semanticModel.GetDeclaredSymbol(variable);
            return null;
        }

        return semanticModel.GetDeclaredSymbol(node);
    }

    private static DocumentationCommentTriviaSyntax? GetDocComment(SyntaxNode node)
    {
        foreach (SyntaxTrivia trivia in node.GetLeadingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                if (trivia.GetStructure() is DocumentationCommentTriviaSyntax docComment)
                    return docComment;
            }
        }

        return null;
    }

    private static bool HasInheritdoc(DocumentationCommentTriviaSyntax docComment)
    {
        foreach (XmlNodeSyntax xmlNode in docComment.Content)
        {
            if (xmlNode is XmlEmptyElementSyntax emptyElement &&
                emptyElement.Name.LocalName.Text == "inheritdoc")
                return true;

            if (xmlNode is XmlElementSyntax element &&
                element.StartTag?.Name?.LocalName.Text == "inheritdoc")
                return true;
        }

        return false;
    }

    private static bool HasNonEmptyTag(DocumentationCommentTriviaSyntax? docComment, string tagName)
    {
        if (docComment is null)
            return false;

        foreach (XmlNodeSyntax xmlNode in docComment.Content)
        {
            if (xmlNode is not XmlElementSyntax element)
                continue;

            if (element.StartTag?.Name?.LocalName.Text != tagName)
                continue;

            if (!IsElementContentEmpty(element))
                return true;
        }

        return false;
    }

    private static bool HasNonEmptyNamedTag(DocumentationCommentTriviaSyntax docComment, string tagName, string name)
    {
        foreach (XmlNodeSyntax xmlNode in docComment.Content)
        {
            if (xmlNode is not XmlElementSyntax element)
                continue;

            if (element.StartTag?.Name?.LocalName.Text != tagName)
                continue;

            if (GetNameAttributeValue(element) != name)
                continue;

            if (!IsElementContentEmpty(element))
                return true;
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
            }
            else
            {
                // Any non-text content (e.g. <see>, <paramref>) counts as non-empty
                return false;
            }
        }

        return true;
    }

    private static string? GetNameAttributeValue(XmlElementSyntax element)
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

    private static Location GetNameLocation(SyntaxNode node) => node switch
    {
        TypeDeclarationSyntax typeDecl => typeDecl.Identifier.GetLocation(),
        EnumDeclarationSyntax enumDecl => enumDecl.Identifier.GetLocation(),
        DelegateDeclarationSyntax delegateDecl => delegateDecl.Identifier.GetLocation(),
        MethodDeclarationSyntax methodDecl => methodDecl.Identifier.GetLocation(),
        PropertyDeclarationSyntax propDecl => propDecl.Identifier.GetLocation(),
        EventDeclarationSyntax eventDecl => eventDecl.Identifier.GetLocation(),
        IndexerDeclarationSyntax indexerDecl => indexerDecl.ThisKeyword.GetLocation(),
        ConstructorDeclarationSyntax ctorDecl => ctorDecl.Identifier.GetLocation(),
        EnumMemberDeclarationSyntax enumMember => enumMember.Identifier.GetLocation(),
        FieldDeclarationSyntax fieldDecl when fieldDecl.Declaration.Variables.Count > 0
            => fieldDecl.Declaration.Variables[0].Identifier.GetLocation(),
        EventFieldDeclarationSyntax eventFieldDecl when eventFieldDecl.Declaration.Variables.Count > 0
            => eventFieldDecl.Declaration.Variables[0].Identifier.GetLocation(),
        _ => node.GetLocation(),
    };

    private static string GetMemberName(SyntaxNode node) => node switch
    {
        TypeDeclarationSyntax typeDecl => typeDecl.Identifier.Text,
        EnumDeclarationSyntax enumDecl => enumDecl.Identifier.Text,
        DelegateDeclarationSyntax delegateDecl => delegateDecl.Identifier.Text,
        MethodDeclarationSyntax methodDecl => methodDecl.Identifier.Text,
        PropertyDeclarationSyntax propDecl => propDecl.Identifier.Text,
        EventDeclarationSyntax eventDecl => eventDecl.Identifier.Text,
        IndexerDeclarationSyntax => "this",
        ConstructorDeclarationSyntax ctorDecl => ctorDecl.Identifier.Text,
        EnumMemberDeclarationSyntax enumMember => enumMember.Identifier.Text,
        FieldDeclarationSyntax fieldDecl when fieldDecl.Declaration.Variables.Count > 0
            => fieldDecl.Declaration.Variables[0].Identifier.Text,
        EventFieldDeclarationSyntax eventFieldDecl when eventFieldDecl.Declaration.Variables.Count > 0
            => eventFieldDecl.Declaration.Variables[0].Identifier.Text,
        _ => string.Empty,
    };

    private static bool RequiresReturnsTag(SyntaxNode node, SemanticModel semanticModel)
    {
        if (node is MethodDeclarationSyntax methodDecl)
        {
            // void methods don't need a <returns> tag
            if (methodDecl.ReturnType is PredefinedTypeSyntax pt &&
                pt.Keyword.IsKind(SyntaxKind.VoidKeyword))
                return false;

            // Non-generic Task and ValueTask are effectively void and don't need a <returns> tag.
            // Quick syntactic check covers the most common unqualified form.
            if (methodDecl.ReturnType is IdentifierNameSyntax idName &&
                idName.Identifier.Text is "Task" or "ValueTask")
                return false;

            // Semantic fallback handles fully-qualified or aliased forms such as
            // System.Threading.Tasks.Task or a using alias for Task.
            ITypeSymbol? returnType = semanticModel.GetTypeInfo(methodDecl.ReturnType).Type;
            if (IsNonGenericTaskOrValueTask(returnType))
                return false;

            return true;
        }

        return node is PropertyDeclarationSyntax or IndexerDeclarationSyntax;
    }

    private static bool IsNonGenericTaskOrValueTask(ITypeSymbol? typeSymbol)
    {
        if (typeSymbol is null || typeSymbol is INamedTypeSymbol { IsGenericType: true })
            return false;

        return typeSymbol.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
               typeSymbol.Name is "Task" or "ValueTask";
    }

    private static SeparatedSyntaxList<ParameterSyntax> GetParameters(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.ParameterList.Parameters,
        ConstructorDeclarationSyntax ctor => ctor.ParameterList.Parameters,
        IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
        DelegateDeclarationSyntax delegateDecl => delegateDecl.ParameterList.Parameters,
        _ => default,
    };

    private static SeparatedSyntaxList<TypeParameterSyntax> GetTypeParameters(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.TypeParameterList?.Parameters ?? default,
        TypeDeclarationSyntax typeDecl => typeDecl.TypeParameterList?.Parameters ?? default,
        DelegateDeclarationSyntax delegateDecl => delegateDecl.TypeParameterList?.Parameters ?? default,
        _ => default,
    };
}
