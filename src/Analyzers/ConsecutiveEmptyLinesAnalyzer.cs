using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

/// <summary>
/// Reports diagnostics for more than one consecutive empty line.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConsecutiveEmptyLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML005";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Avoid consecutive empty lines",
        messageFormat: "More than one consecutive empty line is not allowed",
        category: "Formatting",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Code should not contain more than one consecutive empty line.");

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
        TextLineCollection lines = sourceText.Lines;

        for (int i = 1; i < lines.Count; i++)
        {
            if (!IsEmpty(lines[i - 1]) || !IsEmpty(lines[i]))
                continue;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, Location.Create(context.Tree, lines[i].Span)));
        }
    }

    private static bool IsEmpty(TextLine line)
    {
        return string.IsNullOrWhiteSpace(line.ToString());
    }
}
