using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

/// <summary>
/// Flags Blazor Razor components that contain large <c>@code</c> blocks.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlazorCodeBehindAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID for this analyzer.</summary>
    public const string DiagnosticId = "XML008";

    private const int MaxNonEmptyLines = 20;

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: "Large @code block should be moved to code-behind",
        messageFormat: "The @code block contains {0} non-empty lines and should be moved to a .razor.cs code-behind file",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Blazor components with large @code blocks should be refactored into code-behind files.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterAdditionalFileAction(AnalyzeAdditionalFile);
    }

    private static void AnalyzeAdditionalFile(AdditionalFileAnalysisContext context)
    {
        if (!context.AdditionalFile.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            return;

        SourceText? text = context.AdditionalFile.GetText(context.CancellationToken);
        if (text is null)
            return;

        string content = text.ToString();
        int searchIndex = 0;

        while (searchIndex < content.Length)
        {
            int codeDirectiveIndex = content.IndexOf("@code", searchIndex, StringComparison.Ordinal);
            if (codeDirectiveIndex < 0)
                break;

            int openingBraceIndex = FindOpeningBrace(content, codeDirectiveIndex + "@code".Length);
            if (openingBraceIndex < 0)
            {
                searchIndex = codeDirectiveIndex + "@code".Length;
                continue;
            }

            int closingBraceIndex = FindClosingBrace(content, openingBraceIndex);
            if (closingBraceIndex < 0)
                break;

            int nonEmptyLines = CountNonEmptyLines(content, openingBraceIndex + 1, closingBraceIndex);
            if (nonEmptyLines > MaxNonEmptyLines)
            {
                var span = new TextSpan(codeDirectiveIndex, "@code".Length);
                var lineSpan = text.Lines.GetLinePositionSpan(span);
                var location = Location.Create(context.AdditionalFile.Path, span, lineSpan);
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, nonEmptyLines));
            }

            searchIndex = closingBraceIndex + 1;
        }
    }

    private static int FindOpeningBrace(string content, int startIndex)
    {
        for (int i = startIndex; i < content.Length; i++)
        {
            char ch = content[i];
            if (char.IsWhiteSpace(ch))
                continue;

            return ch == '{' ? i : -1;
        }

        return -1;
    }

    private static int FindClosingBrace(string content, int openingBraceIndex)
    {
        int depth = 0;
        for (int i = openingBraceIndex; i < content.Length; i++)
        {
            char ch = content[i];
            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int CountNonEmptyLines(string content, int bodyStartIndex, int bodyEndIndex)
    {
        if (bodyEndIndex <= bodyStartIndex)
            return 0;

        string body = content.Substring(bodyStartIndex, bodyEndIndex - bodyStartIndex);
        string[] lines = body.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        int count = 0;
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                count++;
        }

        return count;
    }
}
