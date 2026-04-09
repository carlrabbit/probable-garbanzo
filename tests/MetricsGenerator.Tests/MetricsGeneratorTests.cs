using System.Linq;
using Microsoft.CodeAnalysis;

namespace MetricsGenerator.Tests;

/// <summary>
/// Tests for <see cref="MetricsSourceGenerator"/>.
/// </summary>
public class MetricsGeneratorTests
{
    // -------------------------------------------------------------------------
    // Attribute injection
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_InjectsCollectMetricsAttribute()
    {
        var (diagnostics, sources) = TestHelper.RunGenerator(string.Empty);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(sources, s => s.Contains("class CollectMetricsAttribute"));
    }

    [Fact]
    public void Generator_InjectsMetricAttribute()
    {
        var (diagnostics, sources) = TestHelper.RunGenerator(string.Empty);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(sources, s => s.Contains("class MetricAttribute"));
    }

    [Fact]
    public void Generator_InjectsDumperAttribute()
    {
        var (diagnostics, sources) = TestHelper.RunGenerator(string.Empty);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(sources, s => s.Contains("class DumperAttribute"));
    }

    // -------------------------------------------------------------------------
    // Basic generation
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_GeneratesPartialMethodImplementation_ForSingleMetric()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            [Metric("My description", typeof(MyData))]
            public partial class MyMetrics
            {
                [Dumper]
                public partial void Dump();
            }

            public class MyData { }
            """;

        var (diagnostics, sources) = TestHelper.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string? impl = sources.FirstOrDefault(s => s.Contains("partial class MyMetrics"));
        Assert.NotNull(impl);
        Assert.Contains("partial void Dump()", impl);
        Assert.Contains("System.Console.WriteLine(\"My description\")", impl);
        Assert.Contains("MyData", impl);
    }

    [Fact]
    public void Generator_GeneratesAllMetrics_ForMultipleMetricAttributes()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            [Metric("First metric", typeof(ClassA))]
            [Metric("Second metric", typeof(ClassB))]
            [Metric("Third metric", typeof(ClassC))]
            public partial class MultiMetrics
            {
                [Dumper]
                public partial void Dump();
            }

            public class ClassA { }
            public class ClassB { }
            public class ClassC { }
            """;

        var (diagnostics, sources) = TestHelper.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string? impl = sources.FirstOrDefault(s => s.Contains("partial class MultiMetrics"));
        Assert.NotNull(impl);
        Assert.Contains("System.Console.WriteLine(\"First metric\")", impl);
        Assert.Contains("ClassA", impl);
        Assert.Contains("System.Console.WriteLine(\"Second metric\")", impl);
        Assert.Contains("ClassB", impl);
        Assert.Contains("System.Console.WriteLine(\"Third metric\")", impl);
        Assert.Contains("ClassC", impl);
    }

    // -------------------------------------------------------------------------
    // Namespace handling
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_EmitsCorrectNamespace_WhenClassIsInNamespace()
    {
        const string source = """
            using MetricsGenerator;

            namespace MyApp.Metrics;

            [CollectMetrics]
            [Metric("A metric", typeof(SampleData))]
            public partial class NamespacedMetrics
            {
                [Dumper]
                public partial void Dump();
            }

            public class SampleData { }
            """;

        var (diagnostics, sources) = TestHelper.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string? impl = sources.FirstOrDefault(s => s.Contains("partial class NamespacedMetrics"));
        Assert.NotNull(impl);
        Assert.Contains("namespace MyApp.Metrics", impl);
    }

    [Fact]
    public void Generator_EmitsNoNamespace_WhenClassIsInGlobalNamespace()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            [Metric("A metric", typeof(GlobalData))]
            public partial class GlobalMetrics
            {
                [Dumper]
                public partial void Dump();
            }

            public class GlobalData { }
            """;

        var (diagnostics, sources) = TestHelper.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string? impl = sources.FirstOrDefault(s => s.Contains("partial class GlobalMetrics"));
        Assert.NotNull(impl);
        Assert.DoesNotContain("namespace ", impl);
    }

    // -------------------------------------------------------------------------
    // No-generation cases (generator should stay silent)
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_ProducesNoImplementation_WhenNoDumperMethod()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            [Metric("A metric", typeof(SimpleData))]
            public partial class NoDumperClass
            {
                public void RegularMethod() { }
            }

            public class SimpleData { }
            """;

        var (_, sources) = TestHelper.RunGenerator(source);

        // Only the attribute injection file should be present; no class-specific file.
        Assert.DoesNotContain(sources, s => s.Contains("partial class NoDumperClass"));
    }

    [Fact]
    public void Generator_ProducesNoImplementation_WhenNoMetricAttributes()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            public partial class NoMetricsClass
            {
                [Dumper]
                public partial void Dump();
            }
            """;

        var (_, sources) = TestHelper.RunGenerator(source);

        Assert.DoesNotContain(sources, s => s.Contains("partial class NoMetricsClass"));
    }

    // -------------------------------------------------------------------------
    // Special-character handling
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_EscapesQuotesInDescription()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            [Metric("Say \"hello\"", typeof(EscapeData))]
            public partial class EscapeMetrics
            {
                [Dumper]
                public partial void Dump();
            }

            public class EscapeData { }
            """;

        var (diagnostics, sources) = TestHelper.RunGenerator(source);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        string? impl = sources.FirstOrDefault(s => s.Contains("partial class EscapeMetrics"));
        Assert.NotNull(impl);
        // The generated source must have properly escaped quotes.
        Assert.Contains(@"Say \""hello\""", impl);
    }

    // -------------------------------------------------------------------------
    // Full compilation correctness
    // -------------------------------------------------------------------------

    [Fact]
    public void GeneratedCode_CompilesWithoutErrors()
    {
        const string source = """
            using MetricsGenerator;

            [CollectMetrics]
            [Metric("Compile check", typeof(CompileData))]
            public partial class CompileMetrics
            {
                [Dumper]
                public partial void Dump();
            }

            public class CompileData
            {
                public override string ToString() => "CompileData value";
            }
            """;

        var (_, sources) = TestHelper.RunGenerator(source);

        // Re-compile the original source *plus* every generated file to verify there are
        // no compilation errors in the combined output.
        var allTrees = new[] { source }
            .Concat(sources)
            .Select(s => Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(s));

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(a.Location))
            .Cast<Microsoft.CodeAnalysis.MetadataReference>()
            // Ensure System.Console is available for the generated Console.WriteLine calls.
            .Append(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));

        var finalCompilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "FinalAssembly",
            allTrees,
            references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        var errors = finalCompilation.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);
    }
}
