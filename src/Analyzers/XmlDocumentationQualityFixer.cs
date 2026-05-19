using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XmlDocumentationQualityFixer))]
[Shared]
public sealed class XmlDocumentationQualityFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            XmlDocumentationQualityAnalyzer.MissingOrInvalidReturnsDocumentationId,
            XmlDocumentationQualityAnalyzer.MissingOrInvalidSummaryDocumentationId,
            XmlDocumentationQualityAnalyzer.MissingOrInvalidParamDocumentationId,
            XmlDocumentationQualityAnalyzer.MissingOrInvalidTypeParamDocumentationId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        context.RegisterCodeFix(
            CodeAction.Create(
                "Apply XML documentation rule",
                ct => ApplyFixAsync(context.Document, diagnostic, ct),
                nameof(XmlDocumentationQualityFixer)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
            return document;

        SyntaxNode? declaration = FindDeclarationNode(root, diagnostic.Location.SourceSpan.Start);
        if (declaration is null)
            return document;

        ISymbol? symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);
        if (symbol is null)
            return document;

        string descriptor = XmlDocumentationQualityAnalyzer.GetCanonicalDescriptor(symbol);
        string tagName = GetTagName(diagnostic.Id);
        string rulesFile = GetRulesFileName(diagnostic.Id);
        ImmutableArray<XmlDocumentationRule> rules = XmlDocumentationRules.GetRules(document.Project.AnalyzerOptions, rulesFile);
        XmlDocumentationRule? rule = XmlDocumentationRules.FindFirstMatchingRule(rules, descriptor);
        if (rule is null)
            return document;

        string docText = GetDocumentationText(declaration);
        bool hasDocumentation = docText.Length > 0;
        string lineEnding = docText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string indent = GetIndentation(declaration);
        string commentPrefix = indent + "/// ";

        IReadOnlyList<string> names = tagName switch
        {
            "param" => GetParameterNames(symbol),
            "typeparam" => GetTypeParameterNames(symbol),
            _ => Array.Empty<string>(),
        };

        string updatedText = docText;
        if (tagName is "summary" or "returns")
        {
            updatedText = AddOrReplaceSimpleTag(updatedText, tagName, descriptor, rule, commentPrefix, lineEnding);
        }
        else
        {
            updatedText = AddOrReplaceNamedTags(updatedText, tagName, names, descriptor, rule, commentPrefix, lineEnding);
        }

        if (updatedText == docText)
            return document;

        SourceText sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        SourceText changedText;

        if (hasDocumentation)
        {
            SyntaxTrivia trivia = declaration.GetLeadingTrivia().First(t => t.GetStructure() is DocumentationCommentTriviaSyntax);
            changedText = sourceText.WithChanges(new TextChange(trivia.FullSpan, updatedText));
        }
        else
        {
            TextLine declarationLine = sourceText.Lines.GetLineFromPosition(declaration.SpanStart);
            changedText = sourceText.WithChanges(new TextChange(new TextSpan(declarationLine.Start, 0), updatedText));
        }

        return document.WithText(changedText);
    }

    private static string AddOrReplaceSimpleTag(
        string documentation,
        string tagName,
        string descriptor,
        XmlDocumentationRule rule,
        string commentPrefix,
        string lineEnding)
    {
        Regex tagRegex = new($@"(<{tagName}\b[^>]*>)(.*?)(</{tagName}>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Match match = tagRegex.Match(documentation);
        if (match.Success)
        {
            string currentValue = NormalizeXmlText(match.Groups[2].Value);
            if (!XmlDocumentationRules.IsConfiguredInvalid(rule, currentValue) && !string.IsNullOrWhiteSpace(currentValue))
                return documentation;

            string replacement = XmlDocumentationRules.ApplyReplacement(rule, descriptor, currentValue);
            return tagRegex.Replace(documentation, $"$1{replacement}$3", 1);
        }

        if (!XmlDocumentationRules.IsConfiguredInvalid(rule, string.Empty))
            return documentation;

        string missingReplacement = XmlDocumentationRules.ApplyReplacement(rule, descriptor, string.Empty);
        string newLine = $"{commentPrefix}<{tagName}>{missingReplacement}</{tagName}>{lineEnding}";
        return InsertDocumentationLine(documentation, newLine, tagName);
    }

    private static string AddOrReplaceNamedTags(
        string documentation,
        string tagName,
        IReadOnlyList<string> names,
        string descriptor,
        XmlDocumentationRule rule,
        string commentPrefix,
        string lineEnding)
    {
        string updated = documentation;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Regex namedRegex = new($@"(<{tagName}\b[^>]*name=""(?<name>[^""]+)""[^>]*>)(.*?)(</{tagName}>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        foreach (Match match in namedRegex.Matches(updated).Cast<Match>().ToArray())
        {
            string name = match.Groups["name"].Value;
            if (!names.Contains(name, StringComparer.Ordinal) || !seen.Add(name))
                continue;

            string currentValue = NormalizeXmlText(match.Groups[3].Value);
            if (!XmlDocumentationRules.IsConfiguredInvalid(rule, currentValue) && !string.IsNullOrWhiteSpace(currentValue))
                continue;

            string replacement = XmlDocumentationRules.ApplyReplacement(rule, descriptor, currentValue);
            updated = updated.Replace(match.Value, $"{match.Groups[1].Value}{replacement}{match.Groups[4].Value}");
        }

        foreach (string name in names)
        {
            if (Regex.IsMatch(updated, $@"<{tagName}\b[^>]*name=""{Regex.Escape(name)}""", RegexOptions.CultureInvariant))
                continue;

            if (!XmlDocumentationRules.IsConfiguredInvalid(rule, string.Empty))
                continue;

            string replacement = XmlDocumentationRules.ApplyReplacement(rule, descriptor, string.Empty);
            string newLine = $"{commentPrefix}<{tagName} name=\"{name}\">{replacement}</{tagName}>{lineEnding}";
            updated = InsertDocumentationLine(updated, newLine, tagName);
        }

        return updated;
    }

    private static string InsertDocumentationLine(string documentation, string lineToInsert, string tagName)
    {
        if (documentation.Length == 0)
            return lineToInsert;

        string[] lines = documentation.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var mutable = lines.ToList();

        int insertAt = mutable.Count;
        int tagOrder = GetTagOrder(tagName);

        for (int i = 0; i < mutable.Count; i++)
        {
            int existingOrder = GetExistingLineTagOrder(mutable[i]);
            if (existingOrder > tagOrder)
            {
                insertAt = i;
                break;
            }
        }

        mutable.Insert(insertAt, lineToInsert.TrimEnd('\r', '\n'));
        string lineEnding = documentation.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return string.Join(lineEnding, mutable.Where(static l => l.Length > 0)) + lineEnding;
    }

    private static int GetExistingLineTagOrder(string line)
    {
        if (line.Contains("<summary", StringComparison.Ordinal))
            return 0;
        if (line.Contains("<typeparam", StringComparison.Ordinal))
            return 1;
        if (line.Contains("<param", StringComparison.Ordinal))
            return 2;
        if (line.Contains("<returns", StringComparison.Ordinal))
            return 3;

        return -1;
    }

    private static int GetTagOrder(string tagName) => tagName switch
    {
        "summary" => 0,
        "typeparam" => 1,
        "param" => 2,
        "returns" => 3,
        _ => 4,
    };

    private static SyntaxNode? FindDeclarationNode(SyntaxNode root, int position)
    {
        SyntaxToken token = root.FindToken(position, findInsideTrivia: true);
        return token.Parent?.AncestorsAndSelf().FirstOrDefault(n =>
            n is TypeDeclarationSyntax
                or EnumDeclarationSyntax
                or DelegateDeclarationSyntax
                or MethodDeclarationSyntax
                or PropertyDeclarationSyntax
                or IndexerDeclarationSyntax
                or EventDeclarationSyntax);
    }

    private static string GetDocumentationText(SyntaxNode declaration)
    {
        foreach (SyntaxTrivia trivia in declaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax)
                return trivia.ToFullString();
        }

        return string.Empty;
    }

    private static string GetIndentation(SyntaxNode declaration)
    {
        SyntaxTrivia trivia = declaration.GetLeadingTrivia().FirstOrDefault();
        if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            return trivia.ToFullString();

        SyntaxToken token = declaration.GetFirstToken();
        SyntaxTriviaList lineTrivia = token.LeadingTrivia;
        foreach (SyntaxTrivia t in lineTrivia)
        {
            if (t.IsKind(SyntaxKind.WhitespaceTrivia))
                return t.ToFullString();
        }

        return string.Empty;
    }

    private static string GetTagName(string diagnosticId) => diagnosticId switch
    {
        XmlDocumentationQualityAnalyzer.MissingOrInvalidReturnsDocumentationId => "returns",
        XmlDocumentationQualityAnalyzer.MissingOrInvalidSummaryDocumentationId => "summary",
        XmlDocumentationQualityAnalyzer.MissingOrInvalidParamDocumentationId => "param",
        XmlDocumentationQualityAnalyzer.MissingOrInvalidTypeParamDocumentationId => "typeparam",
        _ => string.Empty,
    };

    private static string GetRulesFileName(string diagnosticId) => diagnosticId switch
    {
        XmlDocumentationQualityAnalyzer.MissingOrInvalidReturnsDocumentationId => XmlDocumentationRules.ReturnsRulesFileName,
        XmlDocumentationQualityAnalyzer.MissingOrInvalidSummaryDocumentationId => XmlDocumentationRules.SummaryRulesFileName,
        XmlDocumentationQualityAnalyzer.MissingOrInvalidParamDocumentationId => XmlDocumentationRules.ParamRulesFileName,
        XmlDocumentationQualityAnalyzer.MissingOrInvalidTypeParamDocumentationId => XmlDocumentationRules.TypeParamRulesFileName,
        _ => string.Empty,
    };

    private static IReadOnlyList<string> GetParameterNames(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.Parameters.Select(static p => p.Name).ToArray(),
        IPropertySymbol property => property.Parameters.Select(static p => p.Name).ToArray(),
        INamedTypeSymbol { TypeKind: TypeKind.Delegate } namedType when namedType.DelegateInvokeMethod is not null
            => namedType.DelegateInvokeMethod.Parameters.Select(static p => p.Name).ToArray(),
        _ => Array.Empty<string>(),
    };

    private static IReadOnlyList<string> GetTypeParameterNames(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.TypeParameters.Select(static p => p.Name).ToArray(),
        INamedTypeSymbol namedType => namedType.TypeParameters.Select(static p => p.Name).ToArray(),
        _ => Array.Empty<string>(),
    };

    private static string NormalizeXmlText(string text) =>
        Regex.Replace(text.Replace("///", string.Empty).Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
}
