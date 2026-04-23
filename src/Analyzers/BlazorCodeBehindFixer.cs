using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Analyzers;

/// <summary>
/// Creates a code-behind file for Blazor components flagged by <see cref="BlazorCodeBehindAnalyzer"/>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlazorCodeBehindFixer))]
[Shared]
public sealed class BlazorCodeBehindFixer : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(BlazorCodeBehindAnalyzer.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics.First();
        string? razorPath = diagnostic.Location.GetLineSpan().Path;

        if (string.IsNullOrWhiteSpace(razorPath) ||
            !razorPath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        string codeBehindFileName = Path.GetFileName(razorPath) + ".cs";
        bool alreadyExists = context.Document.Project.Documents
            .Any(d => string.Equals(d.Name, codeBehindFileName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
            return Task.CompletedTask;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Create code-behind file",
                createChangedSolution: ct => CreateCodeBehindAsync(context.Document, razorPath, ct),
                equivalenceKey: nameof(BlazorCodeBehindFixer)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Solution> CreateCodeBehindAsync(
        Document document,
        string razorPath,
        CancellationToken cancellationToken)
    {
        Project project = document.Project;
        Solution changedSolution = project.Solution;

        string componentName = Path.GetFileNameWithoutExtension(razorPath);
        string className = CreateClassName(componentName);
        string namespaceName = project.DefaultNamespace ?? project.Name;
        string codeBehindFileName = Path.GetFileName(razorPath) + ".cs";
        string codeBehindPath = Path.Combine(Path.GetDirectoryName(razorPath) ?? string.Empty, codeBehindFileName);

        Document? razorDocument = project.Documents.FirstOrDefault(
            d => string.Equals(d.FilePath, razorPath, StringComparison.OrdinalIgnoreCase));

        string movedCodeMembers = string.Empty;
        ImmutableArray<(string TypeName, string PropertyName)> injectDirectives = ImmutableArray<(string TypeName, string PropertyName)>.Empty;
        if (razorDocument is not null)
        {
            SourceText razorText = await razorDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
            string razorContent = razorText.ToString();

            (injectDirectives, movedCodeMembers, string updatedRazor) = TransformRazor(razorText, razorContent);
            changedSolution = changedSolution.WithDocumentText(razorDocument.Id, SourceText.From(updatedRazor, Encoding.UTF8));
        }

        string injectMembers = string.Join(
            "\n",
            injectDirectives.Select(i => $"    [Inject]\n    public {i.TypeName} {i.PropertyName} {{ get; set; }} = default!;"));

        string codeMembers = string.IsNullOrWhiteSpace(movedCodeMembers)
            ? string.Empty
            : "    " + movedCodeMembers.Replace("\n", "\n    ");

        string classBody = string.Empty;
        if (!string.IsNullOrWhiteSpace(injectMembers))
            classBody = injectMembers;

        if (!string.IsNullOrWhiteSpace(codeMembers))
            classBody = string.IsNullOrWhiteSpace(classBody) ? codeMembers : classBody + "\n\n" + codeMembers;

        string content =
$@"using Microsoft.AspNetCore.Components;

namespace {namespaceName};

public partial class {className} : ComponentBase
{{
{classBody}
}}
";

        DocumentId documentId = DocumentId.CreateNewId(project.Id, codeBehindFileName);
        changedSolution = changedSolution.AddDocument(
            documentId,
            codeBehindFileName,
            SourceText.From(content),
            filePath: codeBehindPath);

        return changedSolution;
    }

    private static (ImmutableArray<(string TypeName, string PropertyName)> InjectDirectives, string MovedCodeMembers, string UpdatedRazor) TransformRazor(SourceText text, string content)
    {
        var removeSpans = new List<TextSpan>();
        var injects = ImmutableArray.CreateBuilder<(string TypeName, string PropertyName)>();

        foreach (TextLine line in text.Lines)
        {
            string lineText = line.ToString();
            int nonWhitespaceIndex = 0;
            while (nonWhitespaceIndex < lineText.Length && char.IsWhiteSpace(lineText[nonWhitespaceIndex]))
                nonWhitespaceIndex++;

            if (nonWhitespaceIndex >= lineText.Length)
                continue;

            ReadOnlySpan<char> trimmed = lineText.AsSpan(nonWhitespaceIndex);
            if (!trimmed.StartsWith("@inject".AsSpan(), StringComparison.Ordinal))
                continue;

            int afterDirectiveIndex = nonWhitespaceIndex + "@inject".Length;
            if (afterDirectiveIndex < lineText.Length &&
                !char.IsWhiteSpace(lineText[afterDirectiveIndex]))
            {
                continue;
            }

            string remainder = lineText.Substring(afterDirectiveIndex).Trim();
            string[] tokens = remainder.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
                injects.Add((tokens[0], tokens[1]));

            removeSpans.Add(line.SpanIncludingLineBreak);
        }

        string movedCodeMembers = string.Empty;
        int codeDirectiveIndex = content.IndexOf("@code", StringComparison.Ordinal);
        if (codeDirectiveIndex >= 0 && IsValidCodeDirective(content, codeDirectiveIndex))
        {
            int openingBraceIndex = FindOpeningBrace(content, codeDirectiveIndex + "@code".Length);
            int closingBraceIndex = openingBraceIndex >= 0 ? FindClosingBrace(content, openingBraceIndex) : -1;
            if (openingBraceIndex >= 0 && closingBraceIndex > openingBraceIndex)
            {
                string body = content.Substring(openingBraceIndex + 1, closingBraceIndex - openingBraceIndex - 1);
                movedCodeMembers = NormalizeIndentation(body);
                removeSpans.Add(new TextSpan(codeDirectiveIndex, closingBraceIndex - codeDirectiveIndex + 1));
            }
        }

        string updatedRazor = content;
        foreach (TextSpan span in removeSpans.OrderByDescending(s => s.Start))
            updatedRazor = updatedRazor.Remove(span.Start, span.Length);

        return (injects.ToImmutable(), movedCodeMembers, updatedRazor);
    }

    private static bool IsValidCodeDirective(string content, int codeDirectiveIndex)
    {
        int beforeIndex = codeDirectiveIndex - 1;
        if (beforeIndex >= 0 && (char.IsLetterOrDigit(content[beforeIndex]) || content[beforeIndex] == '_'))
            return false;

        int afterIndex = codeDirectiveIndex + "@code".Length;
        if (afterIndex < content.Length &&
            !char.IsWhiteSpace(content[afterIndex]) &&
            content[afterIndex] != '{')
        {
            return false;
        }

        return true;
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
        const string classPrefix = "class __Generated ";
        string classText = classPrefix + content.Substring(openingBraceIndex);
        CompilationUnitSyntax compilationUnit = SyntaxFactory.ParseCompilationUnit(classText);

        if (compilationUnit.Members.Count == 0 ||
            compilationUnit.Members[0] is not ClassDeclarationSyntax classDeclaration ||
            classDeclaration.CloseBraceToken.IsMissing)
        {
            return -1;
        }

        int relativeCloseBraceIndex = classDeclaration.CloseBraceToken.SpanStart - classPrefix.Length;
        return openingBraceIndex + relativeCloseBraceIndex;
    }

    private static string NormalizeIndentation(string text)
    {
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        int minIndent = int.MaxValue;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent]))
                indent++;

            if (indent < minIndent)
                minIndent = indent;
        }

        if (minIndent == int.MaxValue)
            return string.Empty;

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                lines[i] = string.Empty;
                continue;
            }

            lines[i] = lines[i].Substring(Math.Min(minIndent, lines[i].Length));
        }

        return string.Join("\n", lines).Trim('\n');
    }

    private static string CreateClassName(string componentName)
    {
        if (SyntaxFacts.IsValidIdentifier(componentName))
            return componentName;

        var builder = new StringBuilder(componentName.Length + 16);
        for (int i = 0; i < componentName.Length; i++)
        {
            char ch = componentName[i];
            bool valid = i == 0
                ? SyntaxFacts.IsIdentifierStartCharacter(ch)
                : SyntaxFacts.IsIdentifierPartCharacter(ch);

            builder.Append(valid ? ch : '_');
        }

        if (builder.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(builder[0]))
            builder.Insert(0, '_');

        string sanitized = builder.ToString();
        while (sanitized.Length > 0 && sanitized[0] == '_')
            sanitized = sanitized.Substring(1);

        if (string.IsNullOrWhiteSpace(sanitized) || !SyntaxFacts.IsIdentifierStartCharacter(sanitized[0]))
            sanitized = "RazorComponent" + Math.Abs(componentName.GetHashCode()).ToString();

        return sanitized;
    }
}
