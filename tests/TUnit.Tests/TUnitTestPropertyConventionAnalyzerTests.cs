using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="TUnitTestPropertyConventionAnalyzer"/> and <see cref="TUnitTestPropertyConventionFixer"/>.
/// </summary>
public class TUnitTestPropertyConventionAnalyzerTests
    : TUnitDiagnosticVerifier<TUnitTestPropertyConventionAnalyzer, TUnitTestPropertyConventionFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => TUnitTestPropertyConventionAnalyzer.Rule;

    [Test]
    public async Task NoDiagnostic_WhenRequiredPropertiesAreValid()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyTests
            {
                [Test]
                [Property("TestType", "UnitTest")]
                [Property("TestTargetSpec", "MySpec")]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenRequiredPropertiesAreMissing()
    {
        await VerifyDiagnosticAsync("""
            public class MyTests
            {
                [Test]
                public void [|Run|]() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenTestAttributeIsNotFromTUnit()
    {
        await VerifyNoDiagnosticAsync("""
            using System;

            public sealed class TestAttribute : Attribute
            {
            }

            public sealed class PropertyAttribute : Attribute
            {
                public PropertyAttribute(string name, string value)
                {
                }
            }

            public class MyTests
            {
                [Test]
                [Property("TestType", "UnitTest")]
                [Property("TestTargetSpec", "MySpec")]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Fix_AddsMissingPropertiesWithTbd()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class MyTests
            {
                [Test]
                public void [|Run|]() { }
            }
            """,
            """
            public class MyTests
            {
                [Test]
                [Property("TestType", "TBD")]
                [Property("TestTargetSpec", "TBD")]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Fix_UsesClassAndBaseDefaults()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using Analyzers;

            [TUnitDefaultProperty("TestType", "IntegrationTest")]
            public class BaseTests
            {
            }

            [TUnitDefaultProperty("TestTargetSpec", "BaseSpec")]
            public class MyTests : BaseTests
            {
                [Test]
                public void [|Run|]() { }
            }
            """,
            """
            using Analyzers;

            [TUnitDefaultProperty("TestType", "IntegrationTest")]
            public class BaseTests
            {
            }

            [TUnitDefaultProperty("TestTargetSpec", "BaseSpec")]
            public class MyTests : BaseTests
            {
                [Test]
                [Property("TestType", "IntegrationTest")]
                [Property("TestTargetSpec", "BaseSpec")]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Fix_ReplacesInvalidPropertyValues()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using Analyzers;

            [TUnitDefaultProperty("TestType", "UnitTest")]
            public class MyTests
            {
                [Test]
                [Property("TestType", "Smoke")]
                [Property("TestTargetSpec", "   ")]
                public void [|Run|]() { }
            }
            """,
            """
            using Analyzers;

            [TUnitDefaultProperty("TestType", "UnitTest")]
            public class MyTests
            {
                [Test]
                [Property("TestType", "UnitTest")]
                [Property("TestTargetSpec", "TBD")]
                public void Run() { }
            }
            """);
    }
}
