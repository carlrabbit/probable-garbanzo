using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="TUnitClassTestTypeAnalyzer"/> and <see cref="TUnitClassTestTypeFixer"/>.
/// </summary>
public class TUnitClassTestTypeAnalyzerTests
    : TUnitDiagnosticVerifier<TUnitClassTestTypeAnalyzer, TUnitClassTestTypeFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => TUnitClassTestTypeAnalyzer.Rule;

    [Test]
    public async Task NoDiagnostic_WhenClassHasValidUnitTestType()
    {
        await VerifyNoDiagnosticAsync("""
            [Property("TestType", "unit")]
            public class MyTests
            {
                [Test]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenClassHasValidE2ETestType()
    {
        await VerifyNoDiagnosticAsync("""
            [Property("TestType", "e2e")]
            public class MyTests
            {
                [Test]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenClassIsMissingTestTypeProperty()
    {
        await VerifyDiagnosticAsync("""
            public class [|MyTests|]
            {
                [Test]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenTestTypePropertyHasInvalidValue()
    {
        await VerifyDiagnosticAsync("""
            [Property("TestType", "UnitTest")]
            public class [|MyTests|]
            {
                [Test]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenClassHasNoTestMethods()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyTests
            {
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenTestAttributeIsNotFromTUnit()
    {
        await VerifyNoDiagnosticAsync("""
            using System;

            public sealed class TestAttribute : Attribute { }

            public sealed class PropertyAttribute : Attribute
            {
                public PropertyAttribute(string name, string value) { }
            }

            public class MyTests
            {
                [Test]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Fix_AddsPropertyWithUnknownValueWhenNamespaceDoesNotMatch()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class [|MyTests|]
            {
                [Test]
                public void Run() { }
            }
            """,
            """
            [Property("TestType", "??")]
            public class MyTests
            {
                [Test]
                public void Run() { }
            }
            """);
    }

    [Test]
    public async Task Fix_AddsUnitValueForUnitTestsNamespace()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            namespace MyProject.UnitTests
            {
                public class [|MyTests|]
                {
                    [Test]
                    public void Run() { }
                }
            }
            """,
            """
            namespace MyProject.UnitTests
            {
                [Property("TestType", "unit")]
                public class MyTests
                {
                    [Test]
                    public void Run() { }
                }
            }
            """);
    }

    [Test]
    public async Task Fix_AddsE2EValueForE2ENamespace()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            namespace MyProject.E2E
            {
                public class [|MyTests|]
                {
                    [Test]
                    public void Run() { }
                }
            }
            """,
            """
            namespace MyProject.E2E
            {
                [Property("TestType", "e2e")]
                public class MyTests
                {
                    [Test]
                    public void Run() { }
                }
            }
            """);
    }

    [Test]
    public async Task Fix_ReplacesInvalidTestTypeValue()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            namespace MyProject.UnitTests
            {
                [Property("TestType", "smoke")]
                public class [|MyTests|]
                {
                    [Test]
                    public void Run() { }
                }
            }
            """,
            """
            namespace MyProject.UnitTests
            {
                [Property("TestType", "unit")]
                public class MyTests
                {
                    [Test]
                    public void Run() { }
                }
            }
            """);
    }
}
