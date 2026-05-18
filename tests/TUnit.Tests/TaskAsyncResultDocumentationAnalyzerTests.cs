using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

/// <summary>
/// TUnit tests for <see cref="TaskAsyncResultDocumentationAnalyzer"/> and <see cref="TaskAsyncResultDocumentationFixer"/>.
/// </summary>
public class TaskAsyncResultDocumentationAnalyzerTests
    : TUnitDiagnosticVerifier<TaskAsyncResultDocumentationAnalyzer, TaskAsyncResultDocumentationFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => TaskAsyncResultDocumentationAnalyzer.Rule;

    [Test]
    public async Task Diagnostic_WhenTaskAsyncMethodHasNoReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task Diagnostic_WhenTaskAsyncMethodHasNoDocumentationComment()
    {
        await VerifyDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task Fix_AddsReturnsTag_WhenMissing()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """,
            """
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                /// <returns>A task that represents the asynchronous save operation.</returns>
                public Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task Fix_FillsReturnsTag_WhenEmpty()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                /// <returns></returns>
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """,
            """
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                /// <returns>A task that represents the asynchronous save operation.</returns>
                public Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenReturnsTagHasContent()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                /// <returns>A task that represents the asynchronous save operation.</returns>
                public Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenMethodIsPrivate()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                private Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }
}
