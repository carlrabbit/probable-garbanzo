using System;
using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="AssertArrangeActAnalyzer"/> and <see cref="AssertArrangeActFixer"/>.
/// </summary>
public class AssertArrangeActAnalyzerTests
    : TUnitDiagnosticVerifier<AssertArrangeActAnalyzer, AssertArrangeActFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => AssertArrangeActAnalyzer.Rule;

    [Test]
    public async Task NoDiagnostic_WhenAllCommentsArePresent()
    {
        await VerifyNoDiagnosticAsync("""
            public class MyTests
            {
                [Test]
                public void Run()
                {
                    // Arrange
                    var value = 1;
                    // Act
                    var result = value + 1;
                    // Assert
                    _ = result;
                }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenCommentsAreMissing()
    {
        await VerifyDiagnosticAsync("""
            public class MyTests
            {
                [Test]
                public void [|Run|]()
                {
                    var value = 1;
                }
            }
            """);
    }

    [Test]
    public async Task Fix_AddsCommentsAtMethodStart()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class MyTests
            {
                [Test]
                public void [|Run|]()
                {
                    var value = 1;
                }
            }
            """,
            """
            public class MyTests
            {
                [Test]
                public void Run()
                {
                    // Arrange
                    // Act
                    // Assert
                    var value = 1;
                }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenTestAttributeIsNotFromTUnit()
    {
        await VerifyNoDiagnosticAsync("""
            public sealed class TestAttribute : Attribute
            {
            }

            public class MyTests
            {
                [Test]
                public void Run()
                {
                    var value = 1;
                }
            }
            """);
    }
}
