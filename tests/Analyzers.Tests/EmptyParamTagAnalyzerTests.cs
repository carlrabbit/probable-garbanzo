using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="EmptyParamTagAnalyzer"/> and <see cref="EmptyParamTagFixer"/>.
/// </summary>
public class EmptyParamTagAnalyzerTests
    : XunitDiagnosticVerifier<EmptyParamTagAnalyzer, EmptyParamTagFixer>
{
    /// <inheritdoc/>
    public override CSharpTestOptions Options => CSharpTestOptions.Default
        .WithMetadataReferences(
            CSharpTestOptions.Default.MetadataReferences.Add(
                MetadataReference.CreateFromFile(
                    typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly.Location)));

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => EmptyParamTagAnalyzer.Rule;

    [Fact]
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
            """);
    }

    [Fact]
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
            """);
    }

    [Fact]
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

    [Fact]
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
            """);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoDocComment()
    {
        await VerifyNoDiagnosticAsync("""
            using Microsoft.Extensions.Configuration;

            public class MyClass
            {
                public void MyMethod(IConfiguration configuration) { }
            }
            """);
    }

    [Fact]
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
            """);
    }
}
