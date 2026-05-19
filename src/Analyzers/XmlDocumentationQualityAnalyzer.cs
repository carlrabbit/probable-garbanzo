using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XmlDocumentationQualityAnalyzer : DiagnosticAnalyzer
{
    public const string MissingOrInvalidReturnsDocumentationId = "DOC001";
    public const string MissingOrInvalidSummaryDocumentationId = "DOC002";
    public const string MissingOrInvalidParamDocumentationId = "DOC003";
    public const string MissingOrInvalidTypeParamDocumentationId = "DOC004";

    public static readonly DiagnosticDescriptor MissingOrInvalidReturnsDocumentationRule = new(
        MissingOrInvalidReturnsDocumentationId,
        "Missing or invalid <returns> documentation",
        "Symbol '{0}' has missing or invalid <returns> documentation",
        "Documentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingOrInvalidSummaryDocumentationRule = new(
        MissingOrInvalidSummaryDocumentationId,
        "Missing or invalid <summary> documentation",
        "Symbol '{0}' has missing or invalid <summary> documentation",
        "Documentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingOrInvalidParamDocumentationRule = new(
        MissingOrInvalidParamDocumentationId,
        "Missing or invalid <param> documentation",
        "Symbol '{0}' has missing or invalid <param> documentation",
        "Documentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingOrInvalidTypeParamDocumentationRule = new(
        MissingOrInvalidTypeParamDocumentationId,
        "Missing or invalid <typeparam> documentation",
        "Symbol '{0}' has missing or invalid <typeparam> documentation",
        "Documentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MissingOrInvalidReturnsDocumentationRule,
            MissingOrInvalidSummaryDocumentationRule,
            MissingOrInvalidParamDocumentationRule,
            MissingOrInvalidTypeParamDocumentationRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            ImmutableArray<XmlDocumentationRule> returnsRules =
                XmlDocumentationRules.GetRules(startContext.Options, XmlDocumentationRules.ReturnsRulesFileName);
            ImmutableArray<XmlDocumentationRule> summaryRules =
                XmlDocumentationRules.GetRules(startContext.Options, XmlDocumentationRules.SummaryRulesFileName);
            ImmutableArray<XmlDocumentationRule> paramRules =
                XmlDocumentationRules.GetRules(startContext.Options, XmlDocumentationRules.ParamRulesFileName);
            ImmutableArray<XmlDocumentationRule> typeParamRules =
                XmlDocumentationRules.GetRules(startContext.Options, XmlDocumentationRules.TypeParamRulesFileName);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, summaryRules, paramRules, typeParamRules),
                SymbolKind.NamedType);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, returnsRules, summaryRules, paramRules, typeParamRules),
                SymbolKind.Method);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeProperty(symbolContext, summaryRules, paramRules),
                SymbolKind.Property);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeEvent(symbolContext, summaryRules),
                SymbolKind.Event);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        ImmutableArray<XmlDocumentationRule> summaryRules,
        ImmutableArray<XmlDocumentationRule> paramRules,
        ImmutableArray<XmlDocumentationRule> typeParamRules)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;

        if (!IsAnalyzedType(symbol) || !IsEffectivelyNonPrivate(symbol))
            return;

        string descriptor = GetCanonicalDescriptor(symbol);

        AnalyzeSummary(context, symbol, descriptor, summaryRules);

        if (symbol.TypeParameters.Length > 0)
            AnalyzeTypeParams(context, symbol, descriptor, typeParamRules);

        if (symbol.TypeKind == TypeKind.Delegate)
        {
            AnalyzeParams(context, symbol, descriptor, paramRules, symbol.DelegateInvokeMethod?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty);
        }
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        ImmutableArray<XmlDocumentationRule> returnsRules,
        ImmutableArray<XmlDocumentationRule> summaryRules,
        ImmutableArray<XmlDocumentationRule> paramRules,
        ImmutableArray<XmlDocumentationRule> typeParamRules)
    {
        var symbol = (IMethodSymbol)context.Symbol;

        if (!IsAnalyzedMethod(symbol) || !IsEffectivelyNonPrivate(symbol))
            return;

        string descriptor = GetCanonicalDescriptor(symbol);

        AnalyzeSummary(context, symbol, descriptor, summaryRules);

        if (RequiresReturns(symbol))
            AnalyzeReturns(context, symbol, descriptor, returnsRules);

        if (symbol.Parameters.Length > 0)
            AnalyzeParams(context, symbol, descriptor, paramRules, symbol.Parameters);

        if (symbol.TypeParameters.Length > 0)
            AnalyzeTypeParams(context, symbol, descriptor, typeParamRules);
    }

    private static void AnalyzeProperty(
        SymbolAnalysisContext context,
        ImmutableArray<XmlDocumentationRule> summaryRules,
        ImmutableArray<XmlDocumentationRule> paramRules)
    {
        var symbol = (IPropertySymbol)context.Symbol;

        if (!IsAnalyzedProperty(symbol) || !IsEffectivelyNonPrivate(symbol))
            return;

        string descriptor = GetCanonicalDescriptor(symbol);

        AnalyzeSummary(context, symbol, descriptor, summaryRules);

        if (symbol.IsIndexer)
            AnalyzeParams(context, symbol, descriptor, paramRules, symbol.Parameters);
    }

    private static void AnalyzeEvent(SymbolAnalysisContext context, ImmutableArray<XmlDocumentationRule> summaryRules)
    {
        var symbol = (IEventSymbol)context.Symbol;

        if (symbol.IsImplicitlyDeclared || !IsEffectivelyNonPrivate(symbol))
            return;

        string descriptor = GetCanonicalDescriptor(symbol);
        AnalyzeSummary(context, symbol, descriptor, summaryRules);
    }

    private static void AnalyzeSummary(
        SymbolAnalysisContext context,
        ISymbol symbol,
        string descriptor,
        ImmutableArray<XmlDocumentationRule> rules)
    {
        TagAnalysisResult result = AnalyzeTag(
            symbol,
            "summary",
            descriptor,
            rules,
            context.Compilation,
            context.CancellationToken,
            Array.Empty<string>(),
            resolveFromInheritdoc: true);

        if (!result.IsValid)
        {
            Report(context, MissingOrInvalidSummaryDocumentationRule, symbol, result.Location);
        }
    }

    private static void AnalyzeReturns(
        SymbolAnalysisContext context,
        IMethodSymbol symbol,
        string descriptor,
        ImmutableArray<XmlDocumentationRule> rules)
    {
        TagAnalysisResult result = AnalyzeTag(
            symbol,
            "returns",
            descriptor,
            rules,
            context.Compilation,
            context.CancellationToken,
            Array.Empty<string>(),
            resolveFromInheritdoc: true);

        if (!result.IsValid)
        {
            Report(context, MissingOrInvalidReturnsDocumentationRule, symbol, result.Location);
        }
    }

    private static void AnalyzeParams(
        SymbolAnalysisContext context,
        ISymbol symbol,
        string descriptor,
        ImmutableArray<XmlDocumentationRule> rules,
        ImmutableArray<IParameterSymbol> parameters)
    {
        string[] names = parameters.Select(static p => p.Name).ToArray();

        TagAnalysisResult result = AnalyzeTag(
            symbol,
            "param",
            descriptor,
            rules,
            context.Compilation,
            context.CancellationToken,
            names,
            resolveFromInheritdoc: true);

        if (!result.IsValid)
        {
            Report(context, MissingOrInvalidParamDocumentationRule, symbol, result.Location);
        }
    }

    private static void AnalyzeTypeParams(
        SymbolAnalysisContext context,
        ISymbol symbol,
        string descriptor,
        ImmutableArray<XmlDocumentationRule> rules)
    {
        string[] names = symbol switch
        {
            INamedTypeSymbol namedType => namedType.TypeParameters.Select(static p => p.Name).ToArray(),
            IMethodSymbol method => method.TypeParameters.Select(static p => p.Name).ToArray(),
            _ => Array.Empty<string>(),
        };

        TagAnalysisResult result = AnalyzeTag(
            symbol,
            "typeparam",
            descriptor,
            rules,
            context.Compilation,
            context.CancellationToken,
            names,
            resolveFromInheritdoc: true);

        if (!result.IsValid)
        {
            Report(context, MissingOrInvalidTypeParamDocumentationRule, symbol, result.Location);
        }
    }

    internal static TagAnalysisResult AnalyzeTag(
        ISymbol symbol,
        string tagName,
        string descriptor,
        ImmutableArray<XmlDocumentationRule> rules,
        Compilation compilation,
        System.Threading.CancellationToken cancellationToken,
        IReadOnlyList<string> namedTargets,
        bool resolveFromInheritdoc)
    {
        XmlDocumentationRule? rule = XmlDocumentationRules.FindFirstMatchingRule(rules, descriptor);

        TagAnalysisResult? bestInvalid = null;

        foreach (SyntaxReference syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SyntaxNode declaration = syntaxReference.GetSyntax(cancellationToken);
            if (IsGeneratedSyntaxNode(declaration))
                continue;

            DocumentationCommentTriviaSyntax? docs = GetDocumentationTrivia(declaration);
            if (docs is null)
                continue;

            SemanticModel semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
            DeclarationTagState state = GetDeclarationTagState(docs, semanticModel, tagName, namedTargets);

            if (state.HasStructuralIssue)
            {
                bestInvalid ??= new TagAnalysisResult(false, state.ProblemLocation);
                continue;
            }

            if (state.IsDirectlyValid && !XmlDocumentationRules.IsConfiguredInvalid(rule, state.NormalizedValue))
                return new TagAnalysisResult(true, null);

            if (resolveFromInheritdoc && state.TopLevelInheritdoc is TopLevelInheritdoc inheritdoc)
            {
                if (IsInheritedTagValid(symbol, tagName, namedTargets, inheritdoc.CrefTarget, cancellationToken, depth: 0))
                    return new TagAnalysisResult(true, null);
            }

            if (state.HasDirectTag)
            {
                bestInvalid ??= new TagAnalysisResult(false, state.ProblemLocation);
            }
        }

        if (bestInvalid is not null)
            return bestInvalid.Value;

        if (resolveFromInheritdoc && IsInheritedTagValid(symbol, tagName, namedTargets, null, cancellationToken, depth: 0))
            return new TagAnalysisResult(true, null);

        return new TagAnalysisResult(false, null);
    }

    private static DeclarationTagState GetDeclarationTagState(
        DocumentationCommentTriviaSyntax docs,
        SemanticModel semanticModel,
        string tagName,
        IReadOnlyList<string> namedTargets)
    {
        var namedMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var directValues = new List<string>();
        bool hasDirectTag = false;
        Location? problem = null;
        TopLevelInheritdoc? inheritdoc = null;

        foreach (XmlNodeSyntax node in docs.Content)
        {
            if (node is not XmlElementSyntax and not XmlEmptyElementSyntax)
                continue;

            string? nodeName = node switch
            {
                XmlElementSyntax element => element.StartTag?.Name?.LocalName.Text,
                XmlEmptyElementSyntax empty => empty.Name.LocalName.Text,
                _ => null,
            };

            if (string.Equals(nodeName, "inheritdoc", StringComparison.Ordinal) && inheritdoc is null)
            {
                if (TryGetTopLevelInheritdoc(node, semanticModel, out TopLevelInheritdoc parsed))
                    inheritdoc = parsed;
            }

            if (!string.Equals(nodeName, tagName, StringComparison.Ordinal))
                continue;

            hasDirectTag = true;

            if (tagName is "param" or "typeparam")
            {
                string? name = GetNameAttribute(node);
                if (string.IsNullOrEmpty(name))
                {
                    problem ??= node.GetLocation();
                    return new DeclarationTagState(true, false, string.Empty, problem, inheritdoc, true);
                }

                namedMap.TryGetValue(name, out int count);
                namedMap[name] = count + 1;

                if (!namedTargets.Contains(name, StringComparer.Ordinal))
                {
                    problem ??= node.GetLocation();
                    return new DeclarationTagState(true, false, string.Empty, problem, inheritdoc, true);
                }

                if (namedMap[name] > 1)
                {
                    problem ??= node.GetLocation();
                    return new DeclarationTagState(true, false, string.Empty, problem, inheritdoc, true);
                }

                string value = NormalizeNodeText(node);
                if (string.IsNullOrWhiteSpace(value))
                {
                    problem ??= node.GetLocation();
                    return new DeclarationTagState(true, false, value, problem, inheritdoc, false);
                }
            }
            else
            {
                string value = NormalizeNodeText(node);
                directValues.Add(value);
                if (string.IsNullOrWhiteSpace(value))
                {
                    problem ??= node.GetLocation();
                }
            }
        }

        if (tagName is "param" or "typeparam")
        {
            foreach (string name in namedTargets)
            {
                if (!namedMap.ContainsKey(name))
                    return new DeclarationTagState(hasDirectTag, false, string.Empty, null, inheritdoc, false);
            }

            return new DeclarationTagState(hasDirectTag, true, string.Empty, null, inheritdoc, false);
        }

        if (directValues.Count == 0)
            return new DeclarationTagState(hasDirectTag, false, string.Empty, null, inheritdoc, false);

        string normalized = directValues[0];
        return new DeclarationTagState(hasDirectTag, !string.IsNullOrWhiteSpace(normalized), normalized, problem, inheritdoc, false);
    }

    private static bool TryGetTopLevelInheritdoc(XmlNodeSyntax node, SemanticModel semanticModel, out TopLevelInheritdoc inheritdoc)
    {
        inheritdoc = new TopLevelInheritdoc(null);

        if (node is XmlElementSyntax element)
        {
            if (element.StartTag.Attributes.OfType<XmlTextAttributeSyntax>().Any(static a => a.Name.LocalName.Text == "path"))
                return false;

            return true;
        }

        if (node is XmlEmptyElementSyntax emptyElement)
        {
            if (emptyElement.Attributes.OfType<XmlTextAttributeSyntax>().Any(static a => a.Name.LocalName.Text == "path"))
                return false;

            XmlCrefAttributeSyntax? cref = emptyElement.Attributes.OfType<XmlCrefAttributeSyntax>().FirstOrDefault();
            if (cref is null)
                return true;

            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(cref.Cref);
            if (symbolInfo.Symbol is not null)
                inheritdoc = new TopLevelInheritdoc(symbolInfo.Symbol);

            return true;
        }

        return false;
    }

    private static bool IsInheritedTagValid(
        ISymbol currentSymbol,
        string tagName,
        IReadOnlyList<string> names,
        ISymbol? startSymbol,
        System.Threading.CancellationToken cancellationToken,
        int depth)
    {
        if (depth >= 10)
            return false;

        ISymbol? source = startSymbol ?? GetImplicitInheritdocTarget(currentSymbol);
        if (source is null)
            return false;

        string xml = source.GetDocumentationCommentXml(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
            return false;

        try
        {
            XDocument doc = XDocument.Parse(xml);

            if (tagName is "param" or "typeparam")
            {
                foreach (string name in names)
                {
                    XElement[] tags = doc.Descendants(tagName)
                        .Where(e => string.Equals(e.Attribute("name")?.Value, name, StringComparison.Ordinal))
                        .ToArray();

                    if (tags.Length != 1)
                        return false;

                    if (string.IsNullOrWhiteSpace(NormalizeText(tags[0].Value)))
                        return false;
                }

                return names.Count > 0;
            }

            XElement? tag = doc.Descendants(tagName).FirstOrDefault();
            if (tag is not null && !string.IsNullOrWhiteSpace(NormalizeText(tag.Value)))
                return true;

            return IsInheritedTagValid(source, tagName, names, null, cancellationToken, depth + 1);
        }
        catch
        {
            return false;
        }
    }

    private static ISymbol? GetImplicitInheritdocTarget(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol namedType:
                return namedType.BaseType ?? namedType.Interfaces.FirstOrDefault();
            case IMethodSymbol method:
                if (method.OverriddenMethod is not null)
                    return method.OverriddenMethod;

                if (method.ExplicitInterfaceImplementations.Length > 0)
                    return method.ExplicitInterfaceImplementations[0];

                if (method.ContainingType is null)
                    return null;

                foreach (INamedTypeSymbol iface in method.ContainingType.AllInterfaces)
                {
                    foreach (ISymbol member in iface.GetMembers(method.Name))
                    {
                        if (member is not IMethodSymbol ifaceMethod)
                            continue;

                        ISymbol? implementation = method.ContainingType.FindImplementationForInterfaceMember(ifaceMethod);
                        if (SymbolEqualityComparer.Default.Equals(implementation, method))
                            return ifaceMethod;
                    }
                }

                INamedTypeSymbol? baseType = method.ContainingType.BaseType;
                while (baseType is not null)
                {
                    foreach (ISymbol member in baseType.GetMembers(method.Name))
                    {
                        if (member is IMethodSymbol baseMethod && HaveSameSignature(method, baseMethod))
                            return baseMethod;
                    }

                    baseType = baseType.BaseType;
                }

                return null;
            case IPropertySymbol property:
                if (property.OverriddenProperty is not null)
                    return property.OverriddenProperty;

                if (property.ExplicitInterfaceImplementations.Length > 0)
                    return property.ExplicitInterfaceImplementations[0];

                return null;
            case IEventSymbol evt:
                if (evt.OverriddenEvent is not null)
                    return evt.OverriddenEvent;

                if (evt.ExplicitInterfaceImplementations.Length > 0)
                    return evt.ExplicitInterfaceImplementations[0];

                return null;
            default:
                return null;
        }
    }

    private static bool HaveSameSignature(IMethodSymbol left, IMethodSymbol right)
    {
        if (left.Parameters.Length != right.Parameters.Length)
            return false;

        for (int i = 0; i < left.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left.Parameters[i].Type, right.Parameters[i].Type))
                return false;
        }

        return true;
    }

    internal static string GetCanonicalDescriptor(ISymbol symbol)
    {
        var builder = new StringBuilder();
        builder.Append(GetAccessibilityText(symbol.DeclaredAccessibility));

        switch (symbol)
        {
            case INamedTypeSymbol type:
                builder.Append(' ');
                builder.Append(GetTypeKeyword(type));
                builder.Append(' ');
                builder.Append(type.Name);
                if (type.TypeParameters.Length > 0)
                {
                    builder.Append('<');
                    builder.Append(string.Join(", ", type.TypeParameters.Select(static p => p.Name)));
                    builder.Append('>');
                }

                break;

            case IMethodSymbol method:
                string modifier = GetMethodModifier(method);
                if (!string.IsNullOrEmpty(modifier))
                {
                    builder.Append(' ');
                    builder.Append(modifier);
                }

                builder.Append(' ');
                builder.Append(method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                builder.Append(' ');
                builder.Append(method.Name);

                if (method.TypeParameters.Length > 0)
                {
                    builder.Append('<');
                    builder.Append(string.Join(", ", method.TypeParameters.Select(static p => p.Name)));
                    builder.Append('>');
                }

                builder.Append('(');
                builder.Append(string.Join(", ", method.Parameters.Select(static p =>
                    p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + " " + p.Name)));
                builder.Append(')');
                break;

            case IPropertySymbol property:
                builder.Append(' ');
                builder.Append(property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                builder.Append(' ');
                if (property.IsIndexer)
                {
                    builder.Append("this[");
                    builder.Append(string.Join(", ", property.Parameters.Select(static p =>
                        p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + " " + p.Name)));
                    builder.Append(']');
                }
                else
                {
                    builder.Append(property.Name);
                }

                break;

            case IEventSymbol evt:
                builder.Append(" event ");
                builder.Append(evt.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                builder.Append(' ');
                builder.Append(evt.Name);
                break;

            default:
                builder.Append(' ');
                builder.Append(symbol.Name);
                break;
        }

        return builder.ToString();
    }

    private static string GetMethodModifier(IMethodSymbol method)
    {
        if (method.IsStatic)
            return "static";

        if (method.IsAbstract)
            return "abstract";

        if (method.IsVirtual)
            return "virtual";

        if (method.IsOverride)
            return "override";

        return string.Empty;
    }

    private static string GetTypeKeyword(INamedTypeSymbol symbol)
    {
        if (symbol.IsRecord)
        {
            return symbol.IsValueType ? "record struct" : "record class";
        }

        return symbol.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => symbol.TypeKind.ToString().ToLowerInvariant(),
        };
    }

    private static string GetAccessibilityText(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal",
        };

    private static bool IsAnalyzedType(INamedTypeSymbol symbol)
    {
        if (symbol.IsImplicitlyDeclared)
            return false;

        return symbol.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate;
    }

    private static bool IsAnalyzedMethod(IMethodSymbol symbol)
    {
        if (symbol.IsImplicitlyDeclared)
            return false;

        if (symbol.MethodKind is MethodKind.LocalFunction or MethodKind.LambdaMethod or MethodKind.AnonymousFunction or MethodKind.Constructor)
            return false;

        return symbol.MethodKind is MethodKind.Ordinary
            or MethodKind.ExplicitInterfaceImplementation
            or MethodKind.UserDefinedOperator
            or MethodKind.Conversion
            or MethodKind.ReducedExtension;
    }

    private static bool IsAnalyzedProperty(IPropertySymbol symbol) => !symbol.IsImplicitlyDeclared;

    private static bool RequiresReturns(IMethodSymbol symbol) => !symbol.ReturnsVoid;

    private static bool IsEffectivelyNonPrivate(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility == Accessibility.Private)
            return false;

        INamedTypeSymbol? containingType = symbol.ContainingType;
        while (containingType is not null)
        {
            if (containingType.DeclaredAccessibility == Accessibility.Private)
                return false;

            containingType = containingType.ContainingType;
        }

        return true;
    }

    private static bool IsGeneratedSyntaxNode(SyntaxNode node)
    {
        string? path = node.SyntaxTree.FilePath;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
               || path.IndexOf("TemporaryGeneratedFile", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static DocumentationCommentTriviaSyntax? GetDocumentationTrivia(SyntaxNode declaration)
    {
        foreach (SyntaxTrivia trivia in declaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax doc)
                return doc;
        }

        return null;
    }

    private static string? GetNameAttribute(XmlNodeSyntax node)
    {
        SyntaxList<XmlAttributeSyntax> attributes = node switch
        {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax empty => empty.Attributes,
            _ => default,
        };

        foreach (XmlAttributeSyntax attribute in attributes)
        {
            if (attribute is XmlNameAttributeSyntax nameAttr &&
                string.Equals(nameAttr.Name?.LocalName.Text, "name", StringComparison.Ordinal))
            {
                return nameAttr.Identifier.Identifier.ValueText;
            }
        }

        return null;
    }

    private static string NormalizeNodeText(XmlNodeSyntax node)
    {
        string raw = node switch
        {
            XmlElementSyntax element => string.Concat(element.Content.Select(static c => c.ToString())),
            XmlEmptyElementSyntax => string.Empty,
            _ => string.Empty,
        };

        return NormalizeText(raw);
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string withoutDocPrefix = text.Replace("///", string.Empty);
        return Regex.Replace(withoutDocPrefix.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, ISymbol symbol, Location? location)
    {
        Location diagnosticLocation = location
            ?? symbol.Locations.FirstOrDefault(static l => l.IsInSource)
            ?? Location.None;

        context.ReportDiagnostic(Diagnostic.Create(descriptor, diagnosticLocation, symbol.Name));
    }

    internal struct TagAnalysisResult
    {
        public TagAnalysisResult(bool isValid, Location? location)
        {
            IsValid = isValid;
            Location = location;
        }

        public bool IsValid { get; }

        public Location? Location { get; }
    }

    private struct TopLevelInheritdoc
    {
        public TopLevelInheritdoc(ISymbol? crefTarget)
        {
            CrefTarget = crefTarget;
        }

        public ISymbol? CrefTarget { get; }
    }

    private struct DeclarationTagState
    {
        public DeclarationTagState(
            bool hasDirectTag,
            bool isDirectlyValid,
            string normalizedValue,
            Location? problemLocation,
            TopLevelInheritdoc? topLevelInheritdoc,
            bool hasStructuralIssue)
        {
            HasDirectTag = hasDirectTag;
            IsDirectlyValid = isDirectlyValid;
            NormalizedValue = normalizedValue;
            ProblemLocation = problemLocation;
            TopLevelInheritdoc = topLevelInheritdoc;
            HasStructuralIssue = hasStructuralIssue;
        }

        public bool HasDirectTag { get; }

        public bool IsDirectlyValid { get; }

        public string NormalizedValue { get; }

        public Location? ProblemLocation { get; }

        public TopLevelInheritdoc? TopLevelInheritdoc { get; }

        public bool HasStructuralIssue { get; }
    }
}
