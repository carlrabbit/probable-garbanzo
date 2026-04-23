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

namespace TUnit.Tests;

public class BlazorCodeBehindAnalyzerTests
{
    [Test]
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
        Diagnostic[] ruleDiagnostics = diagnostics.Where(d => d.Id == BlazorCodeBehindAnalyzer.DiagnosticId).ToArray();

        await Assert.That(ruleDiagnostics.Length).IsEqualTo(1);
    }

    [Test]
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
        Diagnostic[] ruleDiagnostics = diagnostics.Where(d => d.Id == BlazorCodeBehindAnalyzer.DiagnosticId).ToArray();

        await Assert.That(ruleDiagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Diagnostic_WhenInjectDirectivesExceedLimit()
    {
        const string razor = """
            @inject Service1 Service1
            @inject Service2 Service2
            @inject Service3 Service3
            @inject Service4 Service4
            @inject Service5 Service5
            @inject Service6 Service6
            @inject Service7 Service7
            @inject Service8 Service8
            @inject Service9 Service9
            @inject Service10 Service10
            @inject Service11 Service11
            @inject Service12 Service12
            @inject Service13 Service13
            @inject Service14 Service14
            @inject Service15 Service15
            @inject Service16 Service16
            @inject Service17 Service17
            @inject Service18 Service18
            @inject Service19 Service19
            @inject Service20 Service20
            @inject Service21 Service21
            """;

        ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync("/Test/Counter.razor", razor);
        Diagnostic[] ruleDiagnostics = diagnostics.Where(d => d.Id == BlazorCodeBehindAnalyzer.DiagnosticId).ToArray();

        await Assert.That(ruleDiagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Fix_MovesCodeAndInjectsToCodeBehindAndRemovesFromRazor()
    {
        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId hostDocumentId = DocumentId.CreateNewId(projectId);
        DocumentId razorDocumentId = DocumentId.CreateNewId(projectId);

        const string razorContent = """
            @inject IService MyService
            <h3>Counter</h3>

            @code {
                private int _count = 0;
                private void IncrementCount()
                {
                    _count++;
                }
            }
            """;

        Solution solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, GetAllReferences())
            .AddDocument(hostDocumentId, "Host.cs", SourceText.From("public class Host {}"))
            .AddDocument(razorDocumentId, "Counter.razor", SourceText.From(razorContent), filePath: "/Test/Counter.razor");

        await Assert.That(workspace.TryApplyChanges(solution)).IsEqualTo(true);

        Document hostDocument = workspace.CurrentSolution.GetDocument(hostDocumentId)!;
        Location location = Location.Create(
            "/Test/Counter.razor",
            new TextSpan(0, 5),
            new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 5)));

        Diagnostic diagnostic = Diagnostic.Create(BlazorCodeBehindAnalyzer.Rule, location, "@code block", 21);
        var fixer = new BlazorCodeBehindFixer();
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            hostDocument,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await fixer.RegisterCodeFixesAsync(context);
        await Assert.That(actions.Count).IsEqualTo(1);

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation[] applyChangesOperations = operations.OfType<ApplyChangesOperation>().ToArray();
        await Assert.That(applyChangesOperations.Length).IsEqualTo(1);
        Solution changedSolution = applyChangesOperations[0].ChangedSolution;

        Document[] codeBehindDocuments = changedSolution.GetProject(projectId)!
            .Documents
            .Where(d => d.Name == "Counter.razor.cs")
            .ToArray();
        await Assert.That(codeBehindDocuments.Length).IsEqualTo(1);

        string codeBehind = (await codeBehindDocuments[0].GetTextAsync()).ToString();
        await Assert.That(codeBehind.Contains("public partial class Counter : ComponentBase")).IsEqualTo(true);
        await Assert.That(codeBehind.Contains("[Inject]")).IsEqualTo(true);
        await Assert.That(codeBehind.Contains("public IService MyService { get; set; } = default!;")).IsEqualTo(true);
        await Assert.That(codeBehind.Contains("private int _count = 0;")).IsEqualTo(true);
        await Assert.That(codeBehind.Contains("private void IncrementCount()")).IsEqualTo(true);

        Document razorDocument = changedSolution.GetDocument(razorDocumentId)!;
        string updatedRazor = (await razorDocument.GetTextAsync()).ToString();
        await Assert.That(updatedRazor.Contains("@inject")).IsEqualTo(false);
        await Assert.That(updatedRazor.Contains("@code")).IsEqualTo(false);
        await Assert.That(updatedRazor.Contains("<h3>Counter</h3>")).IsEqualTo(true);
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
