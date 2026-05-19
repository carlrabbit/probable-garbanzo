using System.Threading.Tasks;
using Analyzers;
using Microsoft.CodeAnalysis;
using Roslynator.Testing.CSharp.Xunit;

namespace Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="TaskAsyncResultDocumentationAnalyzer"/> and <see cref="TaskAsyncResultDocumentationFixer"/>.
/// </summary>
public class TaskAsyncResultDocumentationAnalyzerTests
    : XunitDiagnosticVerifier<TaskAsyncResultDocumentationAnalyzer, TaskAsyncResultDocumentationFixer>
{
    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => TaskAsyncResultDocumentationAnalyzer.Rule;

    [Fact]
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

    [Fact]
    public async Task Diagnostic_WhenTaskAsyncMethodHasEmptyReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <summary>Saves the value.</summary>
                /// <returns>   </returns>
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task Fix_CreatesReturnsDocumentation_WhenNoCommentExists()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Threading.Tasks;

            public class MyClass
            {
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """,
            """
            using System.Threading.Tasks;

            public class MyClass
            {
                /// <returns>A task that represents the asynchronous save operation.</returns>
                public Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }

    [Fact]
    public async Task Fix_WorksForInterfaceMethodDeclaration()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Threading.Tasks;

            public interface IMyInterface
            {
                /// <summary>Saves the value.</summary>
                Task [|SaveAsync|]();
            }
            """,
            """
            using System.Threading.Tasks;

            public interface IMyInterface
            {
                /// <summary>Saves the value.</summary>
                /// <returns>A task that represents the asynchronous save operation.</returns>
                Task SaveAsync();
            }
            """);
    }

    [Fact]
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

    [Fact]
    public async Task NoDiagnostic_WhenInheritdocReferencesBaseTypeReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;

            public class BaseClass
            {
                /// <returns>A task that represents the asynchronous save operation.</returns>
                public virtual Task SaveAsync() => Task.CompletedTask;
            }

            public class DerivedClass : BaseClass
            {
                /// <inheritdoc/>
                public override Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }

    [Fact]
    public async Task NoDiagnostic_WhenInheritdocReferencesBaseInterfaceReturnsTag()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;

            public interface IBaseInterface
            {
                /// <returns>A task that represents the asynchronous save operation.</returns>
                Task SaveAsync();
            }

            public class Implementation : IBaseInterface
            {
                /// <inheritdoc/>
                public Task SaveAsync() => Task.CompletedTask;
            }
            """);
    }

    [Fact]
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

    [Fact]
    public async Task NoDiagnostic_WhenMethodDoesNotReturnTask()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                public Task<int> GetValueAsync() => Task.FromResult(0);
            }
            """);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodNameDoesNotEndWithAsync()
    {
        await VerifyNoDiagnosticAsync("""
            using System.Threading.Tasks;

            public class MyClass
            {
                public Task Save() => Task.CompletedTask;
            }
            """);
    }
}
