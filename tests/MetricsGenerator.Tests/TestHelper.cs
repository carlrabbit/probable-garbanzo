using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MetricsGenerator.Tests;

/// <summary>
/// Helpers for running <see cref="MetricsSourceGenerator"/> against in-memory source code in tests.
/// </summary>
internal static class TestHelper
{
    /// <summary>
    /// Compiles <paramref name="source"/> together with the source generator and returns any
    /// diagnostics produced by the generator, plus every source file that was added by it.
    /// </summary>
    public static (IReadOnlyList<Diagnostic> Diagnostics, string[] GeneratedSources) RunGenerator(
        string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Use the assemblies already loaded into the test process as metadata references so
        // that the test compilation can resolve mscorlib / System.Runtime etc.
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MetricsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        // Collect every syntax tree that was *added* by the generator (i.e. not the original one).
        var generatedSources = outputCompilation.SyntaxTrees
            .Where(t => t != syntaxTree)
            .Select(t => t.GetText().ToString())
            .ToArray();

        return (diagnostics, generatedSources);
    }
}
