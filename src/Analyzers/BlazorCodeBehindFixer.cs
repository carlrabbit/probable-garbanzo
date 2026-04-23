using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
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

    private static Task<Solution> CreateCodeBehindAsync(
        Document document,
        string razorPath,
        CancellationToken cancellationToken)
    {
        Project project = document.Project;
        Solution solution = project.Solution;

        string componentName = Path.GetFileNameWithoutExtension(razorPath);
        string className = SyntaxFacts.IsValidIdentifier(componentName) ? componentName : "RazorComponent";
        string namespaceName = project.DefaultNamespace ?? project.Name;
        string codeBehindFileName = Path.GetFileName(razorPath) + ".cs";
        string codeBehindPath = Path.Combine(Path.GetDirectoryName(razorPath) ?? string.Empty, codeBehindFileName);

        string content =
$@"using Microsoft.AspNetCore.Components;

namespace {namespaceName};

public partial class {className} : ComponentBase
{{
}}
";

        DocumentId documentId = DocumentId.CreateNewId(project.Id, codeBehindFileName);
        Solution changedSolution = solution.AddDocument(
            documentId,
            codeBehindFileName,
            SourceText.From(content),
            filePath: codeBehindPath);

        return Task.FromResult(changedSolution);
    }
}
