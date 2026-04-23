using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Analyzers.Tests;

public class BlazorCodeBehindAnalyzerTests
{
    [Fact]
    public async Task Diagnostic_WhenCodeBlockHasMoreThan20NonEmptyLines()
    {
        const string razor = """
            <h3>Counter</h3>

            @code {
                private int _value1 = 1;
                private int _value2 = 2;
                private int _value3 = 3;
                private int _value4 = 4;
                private int _value5 = 5;
                private int _value6 = 6;
                private int _value7 = 7;
                private int _value8 = 8;
                private int _value9 = 9;
                private int _value10 = 10;
                private int _value11 = 11;
                private int _value12 = 12;
                private int _value13 = 13;
                private int _value14 = 14;
                private int _value15 = 15;
                private int _value16 = 16;
                private int _value17 = 17;
                private int _value18 = 18;
                private int _value19 = 19;
                private int _value20 = 20;
                private int _value21 = 21;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync("/Test/Counter.razor", razor);

        _ = Assert.Single(diagnostics.Where(d => d.Id == BlazorCodeBehindAnalyzer.DiagnosticId));
    }

    [Fact]
    public async Task NoDiagnostic_WhenCodeBlockHas20OrFewerNonEmptyLines()
    {
        const string razor = """
            @code {
                private int _value1 = 1;
                private int _value2 = 2;
                private int _value3 = 3;
                private int _value4 = 4;
                private int _value5 = 5;
                private int _value6 = 6;
                private int _value7 = 7;
                private int _value8 = 8;
                private int _value9 = 9;
                private int _value10 = 10;
                private int _value11 = 11;
                private int _value12 = 12;
                private int _value13 = 13;
                private int _value14 = 14;
                private int _value15 = 15;
                private int _value16 = 16;
                private int _value17 = 17;
                private int _value18 = 18;
                private int _value19 = 19;
                private int _value20 = 20;
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync("/Test/Counter.razor", razor);

        Assert.Empty(diagnostics.Where(d => d.Id == BlazorCodeBehindAnalyzer.DiagnosticId));
    }

    [Fact]
    public async Task Fix_CreatesRazorCodeBehindDocument()
    {
        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId hostDocumentId = DocumentId.CreateNewId(projectId);

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, GetAllReferences())
            .AddDocument(hostDocumentId, "Host.cs", SourceText.From("public class Host {}"));

        Assert.True(workspace.TryApplyChanges(solution));

        Document hostDocument = workspace.CurrentSolution.GetDocument(hostDocumentId)!;
        Location location = Location.Create(
            "/Test/Counter.razor",
            new TextSpan(0, 5),
            new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 5)));

        Diagnostic diagnostic = Diagnostic.Create(BlazorCodeBehindAnalyzer.Rule, location, 21);
        var fixer = new BlazorCodeBehindFixer();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            hostDocument,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await fixer.RegisterCodeFixesAsync(context);

        CodeAction actionToApply = Assert.Single(actions);
        var operations = await actionToApply.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation applyChanges = Assert.Single(operations.OfType<ApplyChangesOperation>());
        Solution changedSolution = applyChanges.ChangedSolution;

        Document codeBehindDocument = Assert.Single(
            changedSolution.GetProject(projectId)!.Documents.Where(d => d.Name == "Counter.razor.cs"));

        string content = (await codeBehindDocument.GetTextAsync()).ToString();
        Assert.Contains("public partial class Counter : ComponentBase", content);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string path, string razorContent)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText("public class Placeholder {}");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            GetAllReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new BlazorCodeBehindAnalyzer();
        var options = new AnalyzerOptions(ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(path, razorContent)));
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new CompilationWithAnalyzersOptions(options, null, true, false));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static IEnumerable<MetadataReference> GetAllReferences() =>
        System.AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
