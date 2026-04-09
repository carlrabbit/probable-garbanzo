using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.TUnit;

namespace TUnit.Tests;

/// <summary>
/// Unit tests for <see cref="EmptyParamTagAnalyzer"/> and <see cref="EmptyParamTagFixer"/>.
/// </summary>
public class EmptyParamTagAnalyzerTests
    : TUnitDiagnosticVerifier<EmptyParamTagAnalyzer, EmptyParamTagFixer>
{
    private static readonly CSharpTestOptions ConfigurationOptions = CSharpTestOptions.Default
        .WithMetadataReferences(
            CSharpTestOptions.Default.MetadataReferences.Add(
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
                    typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly.Location)));

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => EmptyParamTagAnalyzer.Rule;

    [Test]
    public async Task Diagnostic_WhenEmptyParamTagForIConfigurationParameter()
    {
        await VerifyDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// [|<param name="configuration"></param>|]
                public void MyMethod(IConfiguration configuration) { }
            }
            """,
            options: ConfigurationOptions);
    }

    [Test]
    public async Task NoDiagnostic_WhenParamTagHasContent()
    {
        await VerifyNoDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="configuration">The configuration instance.</param>
                public void MyMethod(IConfiguration configuration) { }
            }
            """,
            options: ConfigurationOptions);
    }

    [Test]
    public async Task NoDiagnostic_WhenEmptyParamTagForNonIConfigurationParameter()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="value"></param>
                public void MyMethod(string value) { }
            }
            """);
    }

    [Test]
    public async Task Fix_AddsTbdDescriptionToEmptyParamTag()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// [|<param name="configuration"></param>|]
                public void MyMethod(IConfiguration configuration) { }
            }
            """,
            """
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="configuration">TBD</param>
                public void MyMethod(IConfiguration configuration) { }
            }
            """,
            options: ConfigurationOptions);
    }

    [Test]
    public async Task NoDiagnostic_WhenNoDocComment()
    {
        await VerifyNoDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                public void MyMethod(IConfiguration configuration) { }
            }
            """,
            options: ConfigurationOptions);
    }

    [Test]
    public async Task Diagnostic_WhenMultipleParamsAndIConfigurationEmpty()
    {
        await VerifyDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                /// <summary>Does something.</summary>
                /// <param name="value">A value.</param>
                /// [|<param name="configuration"></param>|]
                public void MyMethod(string value, IConfiguration configuration) { }
            }
            """,
            options: ConfigurationOptions);
    }
}
