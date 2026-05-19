using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

public class XmlDocumentationQualitySummaryAnalyzerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidSummaryDocumentationRule;

    [Test]
    public async Task Diagnostic_ForPublicMethodMissingSummary()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>Type summary.</summary>
            public class C
            {
                public int [|M|]() => 1;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_ForPrivateMethod()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>Type summary.</summary>
            public class C
            {
                private int M() => 1;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_ForNonPrivateMemberInsidePrivateType()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>Type summary.</summary>
            public class C
            {
                /// <summary>Outer summary.</summary>
                private class Nested
                {
                    public int M() => 1;
                }
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_WhenTopLevelInheritdocHasSummary()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>Base type summary.</summary>
            public class BaseType
            {
                /// <summary>Base summary.</summary>
                public virtual int M() => 0;
            }

            /// <summary>Derived type summary.</summary>
            public class DerivedType : BaseType
            {
                /// <inheritdoc />
                public override int M() => 1;
            }
            """);
    }
}

public class XmlDocumentationQualityReturnsAnalyzerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidReturnsDocumentationRule;

    [Test]
    public async Task Diagnostic_ForTaskReturnWithoutReturnsTag()
    {
        await VerifyDiagnosticAsync("""
            using System.Threading.Tasks;

            public class C
            {
                /// <summary>Summary.</summary>
                public Task [|SaveAsync|]() => Task.CompletedTask;
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_ForVoidMethod()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>Type summary.</summary>
            public class C
            {
                /// <summary>Summary.</summary>
                public void M() { }
            }
            """);
    }
}

public class XmlDocumentationQualityParamAnalyzerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidParamDocumentationRule;

    [Test]
    public async Task Diagnostic_ForMissingParamTag()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>Type summary.</summary>
            public class C
            {
                /// <summary>Summary.</summary>
                public void [|M|](int value) { }
            }
            """);
    }

    [Test]
    public async Task Diagnostic_ForDuplicateParamTag()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>Type summary.</summary>
            public class C
            {
                /// <summary>Summary.</summary>
                /// <param name="value">A.</param>
                /// [|<param name="value">B.</param>|]
                public void M(int value) { }
            }
            """);
    }
}

public class XmlDocumentationQualityTypeParamAnalyzerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidTypeParamDocumentationRule;

    [Test]
    public async Task Diagnostic_ForGenericTypeMissingTypeParamTag()
    {
        await VerifyDiagnosticAsync("""
            /// <summary>Summary.</summary>
            public class [|C|]<T>
            {
            }
            """);
    }

    [Test]
    public async Task NoDiagnostic_ForTypeParamThroughInheritdocCref()
    {
        await VerifyNoDiagnosticAsync("""
            /// <summary>Summary.</summary>
            /// <typeparam name="T">Item.</typeparam>
            public class Source<T>
            {
            }

            /// <inheritdoc cref="Source{T}" />
            public class Target<T>
            {
            }
            """);
    }
}
