using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

/// <summary>
/// Reports diagnostics for multiple spaces between tokens.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultipleSpacesBetweenTokensAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML006";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Avoid multiple spaces between tokens",
        messageFormat: "More than one space between tokens is not allowed",
        category: "Formatting",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Code should not contain more than one space between tokens, excluding indentation.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        SourceText sourceText = context.Tree.GetText(context.CancellationToken);
        SyntaxNode? root = context.Tree.GetRoot(context.CancellationToken);
        SyntaxToken[] tokens = root.DescendantTokens(descendIntoTrivia: false).ToArray();

        for (int i = 0; i < tokens.Length - 1; i++)
        {
            SyntaxToken current = tokens[i];
            SyntaxToken next = tokens[i + 1];

            if (current.Span.End >= next.SpanStart)
                continue;

            var span = TextSpan.FromBounds(current.Span.End, next.SpanStart);
            string betweenTokens = sourceText.ToString(span);

            if (betweenTokens.Length <= 1 || betweenTokens.Contains('\r') || betweenTokens.Contains('\n'))
                continue;

            bool onlySpaces = true;
            foreach (char ch in betweenTokens)
            {
                if (ch == ' ')
                    continue;

                onlySpaces = false;
                break;
            }

            if (!onlySpaces)
                continue;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, Location.Create(context.Tree, span)));
        }
    }
}
