using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace TUnit.Tests;

/// <summary>
/// A base class for TUnit tests that verify Roslyn diagnostic analyzers and their code fixes,
/// built on top of raw Roslyn APIs without any Roslynator dependency.
/// </summary>
/// <typeparam name="TAnalyzer">The analyzer type under test.</typeparam>
/// <typeparam name="TFixer">The code fix provider type under test.</typeparam>
public abstract class TUnitDiagnosticVerifier<TAnalyzer, TFixer>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFixer : CodeFixProvider, new()
{
    /// <summary>Gets the diagnostic descriptor that the tests verify.</summary>
    public abstract DiagnosticDescriptor Descriptor { get; }

    /// <summary>
    /// Override to supply additional metadata references (e.g. third-party assemblies) that
    /// the test compilation needs in order to resolve types used in test source snippets.
    /// </summary>
    protected virtual IEnumerable<MetadataReference> AdditionalReferences =>
        Enumerable.Empty<MetadataReference>();

    // ---------------------------------------------------------------------------
    // Protected verify helpers (called from [Test] methods)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Asserts that running the analyzer on <paramref name="markedSource"/> produces exactly
    /// the diagnostics indicated by <c>[|…|]</c> span markers in the source.
    /// </summary>
    protected async Task VerifyDiagnosticAsync(string markedSource)
    {
        var (source, expectedSpans) = ParseMarkers(markedSource);
        var diagnostics = await GetDiagnosticsAsync(source);

        var ruleDiagnostics = diagnostics
            .Where(d => d.Id == Descriptor.Id)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToArray();

        await Assert.That(ruleDiagnostics.Length).IsEqualTo(expectedSpans.Length);

        for (int i = 0; i < expectedSpans.Length; i++)
        {
            await Assert.That(ruleDiagnostics[i].Location.SourceSpan).IsEqualTo(expectedSpans[i]);
        }
    }

    /// <summary>
    /// Asserts that running the analyzer on <paramref name="source"/> produces no diagnostics
    /// for the rule under test.
    /// </summary>
    protected async Task VerifyNoDiagnosticAsync(string source)
    {
        var diagnostics = await GetDiagnosticsAsync(source);
        var ruleDiagnostics = diagnostics.Where(d => d.Id == Descriptor.Id).ToArray();
        await Assert.That(ruleDiagnostics.Length).IsEqualTo(0);
    }

    /// <summary>
    /// Asserts that the analyzer reports diagnostics on <paramref name="markedSource"/> and
    /// that after applying the code fix the resulting source matches <paramref name="fixedSource"/>.
    /// </summary>
    protected async Task VerifyDiagnosticAndFixAsync(string markedSource, string fixedSource)
    {
        var (source, _) = ParseMarkers(markedSource);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, GetAllReferences())
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        if (!workspace.TryApplyChanges(solution))
            throw new InvalidOperationException("Failed to apply changes to the AdhocWorkspace.");

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var compilation = await document.Project.GetCompilationAsync();

        var analyzer = new TAnalyzer();
        var compilationWithAnalyzers = compilation!.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var allDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        var ruleDiagnostics = allDiagnostics
            .Where(d => d.Id == Descriptor.Id)
            .ToArray();

        await Assert.That(ruleDiagnostics.Length).IsGreaterThan(0);

        var fixer = new TFixer();
        var currentDocument = document;

        foreach (var diagnostic in ruleDiagnostics)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                currentDocument,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None);

            await fixer.RegisterCodeFixesAsync(context);

            if (actions.Count == 0)
                continue;

            var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
            var applyOp = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
            if (applyOp is null)
                continue;

            currentDocument = applyOp.ChangedSolution.GetDocument(currentDocument.Id)!;
        }

        var actualText = await currentDocument.GetTextAsync();
        await Assert.That(actualText.ToString().Trim()).IsEqualTo(fixedSource.Trim());
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private IEnumerable<MetadataReference> GetAllReferences() =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat(AdditionalReferences);

    private async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            GetAllReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new TAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Extracts <c>[|…|]</c> span markers from <paramref name="markedSource"/>, returning the
    /// clean source text and the <see cref="TextSpan"/> for each marker.
    /// </summary>
    private static (string source, TextSpan[] spans) ParseMarkers(string markedSource)
    {
        var spans = new List<TextSpan>();
        var sb = new StringBuilder();
        int pos = 0;

        while (pos < markedSource.Length)
        {
            int openMarker = markedSource.IndexOf("[|", pos, StringComparison.Ordinal);
            if (openMarker < 0)
            {
                sb.Append(markedSource, pos, markedSource.Length - pos);
                break;
            }

            sb.Append(markedSource, pos, openMarker - pos);
            int spanStart = sb.Length;

            int contentStart = openMarker + 2;
            int closeMarker = markedSource.IndexOf("|]", contentStart, StringComparison.Ordinal);
            if (closeMarker < 0)
                throw new InvalidOperationException("Unmatched [| marker in test source.");

            sb.Append(markedSource, contentStart, closeMarker - contentStart);
            spans.Add(TextSpan.FromBounds(spanStart, sb.Length));

            pos = closeMarker + 2;
        }

        return (sb.ToString(), spans.ToArray());
    }
}
