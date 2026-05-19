using Analyzers;
using Microsoft.CodeAnalysis;

namespace TUnit.Tests;

public class XmlDocumentationQualitySummaryFixerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidSummaryDocumentationRule;

    protected override IEnumerable<(string fileName, string content)> AdditionalFiles =>
        new[]
        {
            ("XmlDocs.Summary.rules", """
                # comments ignored
                ^public .*;^\s*$;Summary from config.

                .*;^\s*$;Fallback summary.
                """),
        };

    [Test]
    public async Task Fix_InsertsMissingSummary_FromConfig()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            /// <summary>Type summary.</summary>
            public class C
            {
                public int [|M|]() => 1;
            }
            """,
            """
            /// <summary>Type summary.</summary>
            public class C
            {
                /// <summary>Summary from config.</summary>
                public int M() => 1;
            }
            """);
    }

    [Test]
    public async Task Fix_ReplacesInvalidSummary_FromConfig()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            /// <summary>Type summary.</summary>
            public class C
            {
                /// [|<summary>   </summary>|]
                public int M() => 1;
            }
            """,
            """
            /// <summary>Type summary.</summary>
            public class C
            {
                /// <summary>Summary from config.</summary>
                public int M() => 1;
            }
            """);
    }
}

public class XmlDocumentationQualityReturnsFixerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidReturnsDocumentationRule;

    protected override IEnumerable<(string fileName, string content)> AdditionalFiles =>
        new[]
        {
            ("XmlDocs.Returns.rules", """
                ^public (?<ret>[^ ]+) .*;^\s*$;Return ${ret}.
                """),
        };

    [Test]
    public async Task Fix_InsertsMissingReturns_UsingCaptureGroup()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class C
            {
                /// <summary>Summary.</summary>
                public int [|M|]() => 1;
            }
            """,
            """
            public class C
            {
                /// <summary>Summary.</summary>
                /// <returns>Return int.</returns>
                public int M() => 1;
            }
            """);
    }
}

public class XmlDocumentationQualityParamFixerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidParamDocumentationRule;

    protected override IEnumerable<(string fileName, string content)> AdditionalFiles =>
        new[]
        {
            ("XmlDocs.Param.rules", """
                .*;^\s*$;Parameter docs.
                """),
        };

    [Test]
    public async Task Fix_InsertsMissingParam()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class C
            {
                /// <summary>Summary.</summary>
                public void [|M|](int value) { }
            }
            """,
            """
            public class C
            {
                /// <summary>Summary.</summary>
                /// <param name="value">Parameter docs.</param>
                public void M(int value) { }
            }
            """);
    }
}

public class XmlDocumentationQualityTypeParamFixerTests
    : TUnitDiagnosticVerifier<XmlDocumentationQualityAnalyzer, XmlDocumentationQualityFixer>
{
    public override DiagnosticDescriptor Descriptor => XmlDocumentationQualityAnalyzer.MissingOrInvalidTypeParamDocumentationRule;

    protected override IEnumerable<(string fileName, string content)> AdditionalFiles =>
        new[]
        {
            ("XmlDocs.TypeParam.rules", """
                .*;^\s*$;Type parameter docs.
                """),
        };

    [Test]
    public async Task Fix_InsertsMissingTypeParam()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            /// <summary>Summary.</summary>
            public class [|C|]<T>
            {
            }
            """,
            """
            /// <summary>Summary.</summary>
            /// <typeparam name="T">Type parameter docs.</typeparam>
            public class C<T>
            {
            }
            """);
    }
}
